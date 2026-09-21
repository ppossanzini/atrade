using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AutoTrade.Trading.Handlers.Analysis
{
  /// <summary>
  /// Local model over the Ollama HTTP API. The engine is asked for a JSON object whose shape is declared as a
  /// schema, and the answer is then checked field by field: the schema raises the chance of a usable answer,
  /// it is not the thing that makes the answer trustworthy. Everything the model returns is treated as a claim
  /// to be validated, so an unusable answer becomes no opinion rather than a weak one.
  /// </summary>
  public class OllamaAnalysisClient : IOllamaAnalysisClient
  {
    private const string TagsPath = "/api/tags";
    private const string GeneratePath = "/api/generate";

    /// <summary>
    /// Schema handed to the engine. The bias enum is spelled out because an allowlist in the request is worth
    /// more than a sentence in the prompt, and the rationale bound is stated so the model does not have to guess
    /// what "short" means.
    /// </summary>
    private const string ResponseSchema =
      "{\"type\":\"object\","
      + "\"properties\":{"
      + "\"symbol\":{\"type\":\"string\"},"
      + "\"bias\":{\"type\":\"string\",\"enum\":[\"Bullish\",\"Bearish\",\"Neutral\"]},"
      + "\"confidence\":{\"type\":\"number\"},"
      + "\"rationale\":{\"type\":\"string\"}"
      + "},"
      + "\"required\":[\"symbol\",\"bias\",\"confidence\",\"rationale\"]}";

    private readonly AnalysisOptions options;
    private readonly HttpClient httpClient;
    private volatile bool modelIsPresent;

    public OllamaAnalysisClient(AnalysisOptions options, HttpClient httpClient)
    {
      this.options = options ?? throw new ArgumentNullException(nameof(options));
      this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Configured and confirmed present. A call that fails afterwards is reported per call, so this stays a
    /// statement about the configuration rather than a promise about the next answer.
    /// </summary>
    public bool IsAvailable
    {
      get { return options.IsConfigured && modelIsPresent; }
    }

    public async Task EnsureModelIsPresentAsync(CancellationToken cancellationToken)
    {
      if (!options.IsConfigured)
      {
        return;
      }

      string body;

      try
      {
        using (HttpResponseMessage response = await httpClient.GetAsync(options.EffectiveEndpoint + TagsPath, cancellationToken))
        {
          if (!response.IsSuccessStatusCode)
          {
            throw new InvalidOperationException("The local model engine at " + options.EffectiveEndpoint + " answered with status " + (int)response.StatusCode + " while listing its models.");
          }

          body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
      }
      catch (HttpRequestException error)
      {
        throw new InvalidOperationException("The local model engine at " + options.EffectiveEndpoint + " is not reachable. Start it, or leave Trading:Ollama:Provider at None.", error);
      }
      catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
      {
        throw new InvalidOperationException("The local model engine at " + options.EffectiveEndpoint + " did not answer in time while listing its models.");
      }

      if (!ListsModel(body, options.Model))
      {
        throw new InvalidOperationException("Trading:Ollama:Model is " + options.Model + " but the local engine does not have it. Pull it first (" + "ollama pull " + options.Model + "), or leave Trading:Ollama:Provider at None.");
      }

      modelIsPresent = true;
    }

    public async Task<AnalysisResult> AnalyseAsync(AnalysisRequest request, CancellationToken cancellationToken)
    {
      if (request == null)
      {
        throw new ArgumentNullException(nameof(request));
      }

      if (string.IsNullOrWhiteSpace(request.Symbol))
      {
        return Unavailable("No symbol was requested, so there is nothing to analyse.");
      }

      if (!IsAvailable)
      {
        return Unavailable("No analysis model is in force: the configured model was not found on the local engine.");
      }

      string requestBody = BuildRequestBody(request);

      try
      {
        using (HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, options.EffectiveEndpoint + GeneratePath))
        {
          message.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

          using (HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken))
          {
            string body = await ReadBoundedBodyAsync(response.Content, cancellationToken);

            if (body == null)
            {
              return Unavailable("The model answer exceeded the configured bound of " + options.EffectiveMaxResponseBytes + " bytes and was discarded.");
            }

            if (!response.IsSuccessStatusCode)
            {
              return Unavailable("The local model engine answered with status " + (int)response.StatusCode + ".");
            }

            return Validate(request, body);
          }
        }
      }
      catch (HttpRequestException error)
      {
        return Unavailable("Transport failure while asking the local model: " + error.Message);
      }
      catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
      {
        return Unavailable("The model did not answer within the configured timeout of " + options.EffectiveTimeoutSeconds + " seconds.");
      }
    }

    /// <summary>
    /// Reads at most the configured bound plus one character. The bound is checked while reading rather than
    /// after, so an answer that never ends cannot be buffered in full before being refused.
    /// </summary>
    private async Task<string> ReadBoundedBodyAsync(HttpContent content, CancellationToken cancellationToken)
    {
      char[] buffer = new char[options.EffectiveMaxResponseBytes];

      using (Stream stream = await content.ReadAsStreamAsync(cancellationToken))
      using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
      {
        int read = await reader.ReadBlockAsync(buffer, 0, buffer.Length);

        // One character past the bound is enough to tell "complete" from "truncated", and it is read rather
        // than tested with EndOfStream, which blocks synchronously inside an async method.
        if (read == buffer.Length)
        {
          int extra = await reader.ReadAsync(buffer, 0, 1);

          if (extra > 0)
          {
            return null;
          }
        }

        return new string(buffer, 0, read);
      }
    }

    private AnalysisResult Validate(AnalysisRequest request, string body)
    {
      string payload;

      try
      {
        using (JsonDocument envelope = JsonDocument.Parse(body))
        {
          JsonElement responseElement;

          if (!envelope.RootElement.TryGetProperty("response", out responseElement) || responseElement.ValueKind != JsonValueKind.String)
          {
            return Rejected("The engine answered outside the expected envelope.");
          }

          payload = responseElement.GetString();
        }
      }
      catch (JsonException)
      {
        return Rejected("The engine returned an unreadable envelope.");
      }

      // JsonElement is only a view over its document, so every value is copied out inside the using block
      // instead of being kept as an element: reading it later would throw on a disposed document.
      string symbol;
      string biasText;
      double confidence;
      string rationale;

      try
      {
        using (JsonDocument document = JsonDocument.Parse(payload))
        {
          if (document.RootElement.ValueKind != JsonValueKind.Object)
          {
            return Rejected("The model answer was not a JSON object.");
          }

          Dictionary<string, JsonElement> fields = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

          foreach (JsonProperty property in document.RootElement.EnumerateObject())
          {
            fields[property.Name] = property.Value;
          }

          JsonElement symbolElement;
          JsonElement biasElement;
          JsonElement confidenceElement;
          JsonElement rationaleElement;

          if (!fields.TryGetValue("symbol", out symbolElement) || symbolElement.ValueKind != JsonValueKind.String)
          {
            return Rejected("The model answer did not carry a symbol.");
          }

          if (!fields.TryGetValue("bias", out biasElement) || biasElement.ValueKind != JsonValueKind.String)
          {
            return Rejected("The model answer did not carry a bias.");
          }

          if (!fields.TryGetValue("confidence", out confidenceElement) || confidenceElement.ValueKind != JsonValueKind.Number)
          {
            return Rejected("The model answer did not carry a numeric confidence.");
          }

          if (!fields.TryGetValue("rationale", out rationaleElement) || rationaleElement.ValueKind != JsonValueKind.String)
          {
            return Rejected("The model answer did not carry a rationale.");
          }

          symbol = symbolElement.GetString();
          biasText = biasElement.GetString();
          confidence = confidenceElement.GetDouble();
          rationale = (rationaleElement.GetString() ?? string.Empty).Trim();
        }
      }
      catch (JsonException)
      {
        return Rejected("The model answer was not valid JSON.");
      }

      if (!string.Equals(symbol, request.Symbol, StringComparison.OrdinalIgnoreCase))
      {
        return Rejected("The model answered about '" + symbol + "' while '" + request.Symbol + "' was under analysis.");
      }

      if (!IsAllowed(request.AllowedSymbols, symbol))
      {
        return Rejected("The model named '" + symbol + "', which is not a symbol this analysis may use.");
      }

      AnalysisBias bias;

      // IsDefined matters: TryParse would accept "7" and hand back an enum value nobody can interpret.
      if (!Enum.TryParse(biasText, ignoreCase: true, result: out bias) || !Enum.IsDefined(typeof(AnalysisBias), bias))
      {
        return Rejected("The model answered with a bias outside the allowed vocabulary.");
      }

      if (double.IsNaN(confidence) || confidence < 0d || confidence > 1d)
      {
        return Rejected("The model reported a confidence outside the 0 to 1 range.");
      }

      if (rationale.Length == 0)
      {
        return Rejected("The model returned an empty rationale.");
      }

      if (rationale.Length > options.EffectiveMaxRationaleLength)
      {
        return Rejected("The model rationale exceeded the configured bound of " + options.EffectiveMaxRationaleLength + " characters.");
      }

      return new AnalysisResult
      {
        IsAvailable = true,
        Opinion = new AnalysisOpinion
        {
          Symbol = symbol,
          Bias = bias,
          Confidence = confidence,
          Rationale = rationale
        }
      };
    }

    private string BuildRequestBody(AnalysisRequest request)
    {
      StringBuilder prompt = new StringBuilder();

      prompt.AppendLine("You are the analysis model of an automated trading system.");
      prompt.AppendLine("Answer with a single JSON object and nothing else.");
      prompt.AppendLine("Instruction version: " + (string.IsNullOrWhiteSpace(request.PromptVersion) ? "unversioned" : request.PromptVersion) + ".");
      prompt.AppendLine("Symbol under analysis: " + request.Symbol + ".");
      prompt.AppendLine("Symbols you may name: " + string.Join(", ", request.AllowedSymbols ?? new List<string>()) + ".");
      prompt.AppendLine("Facts measured by the system, which you may comment on and must not change:");
      prompt.AppendLine(string.IsNullOrWhiteSpace(request.Context) ? "(none)" : request.Context);
      prompt.AppendLine("Return the keys \"symbol\", \"bias\" (Bullish, Bearish or Neutral), \"confidence\" (a number from 0 to 1) and \"rationale\" (one sentence, at most " + options.EffectiveMaxRationaleLength + " characters).");

      Dictionary<string, object> body = new Dictionary<string, object>
      {
        { "model", options.Model },
        { "prompt", prompt.ToString() },
        { "stream", false },
        { "format", JsonSerializer.Deserialize<JsonElement>(ResponseSchema) },
        { "options", new Dictionary<string, object> { { "temperature", options.Temperature } } }
      };

      return JsonSerializer.Serialize(body);
    }

    private static bool ListsModel(string body, string model)
    {
      try
      {
        using (JsonDocument document = JsonDocument.Parse(body))
        {
          JsonElement models;

          if (!document.RootElement.TryGetProperty("models", out models) || models.ValueKind != JsonValueKind.Array)
          {
            return false;
          }

          foreach (JsonElement entry in models.EnumerateArray())
          {
            JsonElement name;

            if (entry.TryGetProperty("name", out name) && name.ValueKind == JsonValueKind.String && string.Equals(name.GetString(), model, StringComparison.OrdinalIgnoreCase))
            {
              return true;
            }
          }

          return false;
        }
      }
      catch (JsonException)
      {
        return false;
      }
    }

    private static bool IsAllowed(List<string> allowedSymbols, string symbol)
    {
      if (allowedSymbols == null || allowedSymbols.Count == 0)
      {
        return false;
      }

      foreach (string candidate in allowedSymbols)
      {
        if (string.Equals(candidate, symbol, StringComparison.OrdinalIgnoreCase))
        {
          return true;
        }
      }

      return false;
    }

    private static AnalysisResult Unavailable(string reason)
    {
      return new AnalysisResult
      {
        IsAvailable = false,
        Opinion = null,
        FailureReason = reason
      };
    }

    private static AnalysisResult Rejected(string reason)
    {
      return new AnalysisResult
      {
        IsAvailable = true,
        Opinion = null,
        FailureReason = reason
      };
    }
  }
}
