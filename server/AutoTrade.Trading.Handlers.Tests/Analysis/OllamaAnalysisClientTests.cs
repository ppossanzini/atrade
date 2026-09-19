using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Handlers.Analysis;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.Analysis
{
  /// <summary>
  /// Covers the analysis seam: what the request asks the engine for, and how an answer is classified. The
  /// engine is a stub, so no model and no network are needed; the point under test is that only an answer
  /// inside the allowlist and the bounds becomes an opinion, and that everything else fails closed.
  /// </summary>
  public class OllamaAnalysisClientTests
  {
    private const string Model = "qwen2.5:3b";
    private const string Symbol = "EURUSD";

    private static AnalysisOptions CreateOptions(int maxResponseBytes = 65536, int maxRationaleLength = 600)
    {
      return new AnalysisOptions
      {
        Provider = AnalysisProviderKind.Ollama,
        Endpoint = "http://127.0.0.1:11434",
        Model = Model,
        TimeoutSeconds = 30,
        MaxResponseBytes = maxResponseBytes,
        MaxRationaleLength = maxRationaleLength,
        Temperature = 0
      };
    }

    private static AnalysisRequest CreateRequest()
    {
      return new AnalysisRequest
      {
        PromptVersion = "analysis-v1",
        Symbol = Symbol,
        AllowedSymbols = new List<string> { "EURUSD", "XAUUSD" },
        Context = "spread 0.8 pips; volatility 0.21 percent"
      };
    }

    /// <summary>Wraps a model answer in the envelope the engine returns.</summary>
    private static string Envelope(string answer)
    {
      return JsonSerializer.Serialize(new Dictionary<string, object>
      {
        { "model", Model },
        { "response", answer },
        { "done", true }
      });
    }

    private static string TagsBody(params string[] models)
    {
      List<Dictionary<string, string>> entries = new List<Dictionary<string, string>>();

      foreach (string model in models)
      {
        entries.Add(new Dictionary<string, string> { { "name", model } });
      }

      return JsonSerializer.Serialize(new Dictionary<string, object> { { "models", entries } });
    }

    private static async Task<OllamaAnalysisClient> CreateReadyClientAsync(StubHttpMessageHandler handler)
    {
      OllamaAnalysisClient client = new OllamaAnalysisClient(CreateOptions(), new HttpClient(handler));

      // The client is usable only after the model was confirmed present, exactly as the host does at startup.
      await client.EnsureModelIsPresentAsync(CancellationToken.None);

      return client;
    }

    [Fact]
    public async Task Analyse_WithAValidAnswer_ReturnsTheOpinion()
    {
      StubHttpMessageHandler handler = StubHttpMessageHandler.Respond(request => request.RequestUri.AbsolutePath == "/api/tags"
        ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(TagsBody(Model)) }
        : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Envelope("{\"symbol\":\"EURUSD\",\"bias\":\"Bullish\",\"confidence\":0.72,\"rationale\":\"Momentum and spread are both favourable.\"}")) });

      OllamaAnalysisClient client = await CreateReadyClientAsync(handler);

      AnalysisResult result = await client.AnalyseAsync(CreateRequest(), CancellationToken.None);

      Assert.True(result.IsAvailable);
      Assert.Null(result.FailureReason);
      Assert.NotNull(result.Opinion);
      Assert.Equal(Symbol, result.Opinion.Symbol);
      Assert.Equal(AnalysisBias.Bullish, result.Opinion.Bias);
      Assert.Equal(0.72d, result.Opinion.Confidence);
      Assert.Equal("Momentum and spread are both favourable.", result.Opinion.Rationale);
    }

    [Fact]
    public async Task Analyse_AsksTheEngineForASingleNonStreamedSchemaAnswer()
    {
      StubHttpMessageHandler handler = StubHttpMessageHandler.Respond(request => request.RequestUri.AbsolutePath == "/api/tags"
        ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(TagsBody(Model)) }
        : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Envelope("{\"symbol\":\"EURUSD\",\"bias\":\"Neutral\",\"confidence\":0.5,\"rationale\":\"No edge.\"}")) });

      OllamaAnalysisClient client = await CreateReadyClientAsync(handler);

      await client.AnalyseAsync(CreateRequest(), CancellationToken.None);

      Assert.Equal(2, handler.Requests.Count);
      Assert.Equal("/api/generate", handler.Requests[1].RequestUri.AbsolutePath);

      string body = handler.Bodies[1];

      using (JsonDocument document = JsonDocument.Parse(body))
      {
        Assert.Equal(Model, document.RootElement.GetProperty("model").GetString());
        Assert.False(document.RootElement.GetProperty("stream").GetBoolean());
        Assert.Equal(0d, document.RootElement.GetProperty("options").GetProperty("temperature").GetDouble());

        // The schema is sent, so the engine can constrain the answer instead of being asked politely.
        JsonElement format = document.RootElement.GetProperty("format");
        Assert.Equal("object", format.GetProperty("type").GetString());

        List<string> biasVocabulary = new List<string>();

        foreach (JsonElement allowed in format.GetProperty("properties").GetProperty("bias").GetProperty("enum").EnumerateArray())
        {
          biasVocabulary.Add(allowed.GetString());
        }

        Assert.Equal(new[] { "Bullish", "Bearish", "Neutral" }, biasVocabulary);

        // The prompt carries the instruction version and the allowlist, which is what makes an old opinion
        // distinguishable from a new one.
        string prompt = document.RootElement.GetProperty("prompt").GetString();
        Assert.Contains("analysis-v1", prompt);
        Assert.Contains("Symbols you may name: EURUSD, XAUUSD.", prompt);
        Assert.Contains("spread 0.8 pips", prompt);
      }
    }

    [Fact]
    public async Task Analyse_WhenTheAnswerNamesAnotherSymbol_IsRejected()
    {
      StubHttpMessageHandler handler = StubHttpMessageHandler.Respond(request => request.RequestUri.AbsolutePath == "/api/tags"
        ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(TagsBody(Model)) }
        : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Envelope("{\"symbol\":\"XAUUSD\",\"bias\":\"Bullish\",\"confidence\":0.8,\"rationale\":\"Gold looks strong.\"}")) });

      OllamaAnalysisClient client = await CreateReadyClientAsync(handler);

      AnalysisResult result = await client.AnalyseAsync(CreateRequest(), CancellationToken.None);

      Assert.True(result.IsAvailable);
      Assert.Null(result.Opinion);
      Assert.Contains("XAUUSD", result.FailureReason);
    }

    [Fact]
    public async Task Analyse_WhenTheAnswerNamesASymbolOutsideTheAllowlist_IsRejected()
    {
      StubHttpMessageHandler handler = StubHttpMessageHandler.Respond(request => request.RequestUri.AbsolutePath == "/api/tags"
        ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(TagsBody(Model)) }
        : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Envelope("{\"symbol\":\"BTCUSD\",\"bias\":\"Bullish\",\"confidence\":0.8,\"rationale\":\"Invented.\"}")) });

      OllamaAnalysisClient client = await CreateReadyClientAsync(handler);

      AnalysisResult result = await client.AnalyseAsync(CreateRequest(), CancellationToken.None);

      Assert.Null(result.Opinion);
      Assert.False(string.IsNullOrWhiteSpace(result.FailureReason));
    }

    [Fact]
    public async Task Analyse_WhenTheBiasIsOutsideTheVocabulary_IsRejected()
    {
      StubHttpMessageHandler handler = StubHttpMessageHandler.Respond(request => request.RequestUri.AbsolutePath == "/api/tags"
        ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(TagsBody(Model)) }
        : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Envelope("{\"symbol\":\"EURUSD\",\"bias\":\"Very Bullish\",\"confidence\":0.8,\"rationale\":\"Strong.\"}")) });

      OllamaAnalysisClient client = await CreateReadyClientAsync(handler);

      AnalysisResult result = await client.AnalyseAsync(CreateRequest(), CancellationToken.None);

      Assert.Null(result.Opinion);
      Assert.Contains("vocabulary", result.FailureReason);
    }

    [Fact]
    public async Task Analyse_WhenTheBiasIsANumber_IsRejected()
    {
      // Enum.TryParse alone would accept "7" and hand back a value nobody can interpret, so the vocabulary is
      // checked, not just the parse.
      StubHttpMessageHandler handler = StubHttpMessageHandler.Respond(request => request.RequestUri.AbsolutePath == "/api/tags"
        ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(TagsBody(Model)) }
        : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Envelope("{\"symbol\":\"EURUSD\",\"bias\":\"7\",\"confidence\":0.8,\"rationale\":\"Numeric bias.\"}")) });

      OllamaAnalysisClient client = await CreateReadyClientAsync(handler);

      AnalysisResult result = await client.AnalyseAsync(CreateRequest(), CancellationToken.None);

      Assert.Null(result.Opinion);
      Assert.Contains("vocabulary", result.FailureReason);
    }

    [Theory]
    [InlineData("1.4")]
    [InlineData("-0.2")]
    public async Task Analyse_WhenTheConfidenceIsOutsideTheRange_IsRejected(string confidence)
    {
      StubHttpMessageHandler handler = StubHttpMessageHandler.Respond(request => request.RequestUri.AbsolutePath == "/api/tags"
        ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(TagsBody(Model)) }
        : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Envelope("{\"symbol\":\"EURUSD\",\"bias\":\"Bullish\",\"confidence\":" + confidence + ",\"rationale\":\"Out of range.\"}")) });

      OllamaAnalysisClient client = await CreateReadyClientAsync(handler);

      AnalysisResult result = await client.AnalyseAsync(CreateRequest(), CancellationToken.None);

      Assert.Null(result.Opinion);
      Assert.Contains("confidence", result.FailureReason);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{\"symbol\":\"EURUSD\",\"bias\":\"Bullish\"}")]
    [InlineData("{\"symbol\":\"EURUSD\",\"bias\":\"Bullish\",\"confidence\":0.5,\"rationale\":\"\"}")]
    public async Task Analyse_WhenTheAnswerIsNotAUsableObject_IsRejected(string answer)
    {
      StubHttpMessageHandler handler = StubHttpMessageHandler.Respond(request => request.RequestUri.AbsolutePath == "/api/tags"
        ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(TagsBody(Model)) }
        : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Envelope(answer)) });

      OllamaAnalysisClient client = await CreateReadyClientAsync(handler);

      AnalysisResult result = await client.AnalyseAsync(CreateRequest(), CancellationToken.None);

      Assert.Null(result.Opinion);
      Assert.False(string.IsNullOrWhiteSpace(result.FailureReason));
    }

    [Fact]
    public async Task Analyse_WhenTheEnvelopeHasNoAnswer_IsRejected()
    {
      StubHttpMessageHandler handler = StubHttpMessageHandler.Respond(request => request.RequestUri.AbsolutePath == "/api/tags"
        ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(TagsBody(Model)) }
        : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"done\":true}") });

      OllamaAnalysisClient client = await CreateReadyClientAsync(handler);

      AnalysisResult result = await client.AnalyseAsync(CreateRequest(), CancellationToken.None);

      Assert.Null(result.Opinion);
      Assert.Contains("envelope", result.FailureReason);
    }

    [Fact]
    public async Task Analyse_WhenTheRationaleExceedsTheBound_IsRejected()
    {
      string rationale = new string('a', 40);

      StubHttpMessageHandler handler = StubHttpMessageHandler.Respond(request => request.RequestUri.AbsolutePath == "/api/tags"
        ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(TagsBody(Model)) }
        : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Envelope("{\"symbol\":\"EURUSD\",\"bias\":\"Bullish\",\"confidence\":0.8,\"rationale\":\"" + rationale + "\"}")) });

      OllamaAnalysisClient client = new OllamaAnalysisClient(CreateOptions(maxRationaleLength: 20), new HttpClient(handler));
      await client.EnsureModelIsPresentAsync(CancellationToken.None);

      AnalysisResult result = await client.AnalyseAsync(CreateRequest(), CancellationToken.None);

      Assert.Null(result.Opinion);
      Assert.Contains("rationale", result.FailureReason);
    }

    [Fact]
    public async Task Analyse_WhenTheAnswerExceedsTheByteBound_IsUnavailable()
    {
      StubHttpMessageHandler handler = StubHttpMessageHandler.Respond(request => request.RequestUri.AbsolutePath == "/api/tags"
        ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(TagsBody(Model)) }
        : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Envelope("{\"symbol\":\"EURUSD\",\"bias\":\"Bullish\",\"confidence\":0.8,\"rationale\":\"" + new string('b', 4096) + "\"}")) });

      OllamaAnalysisClient client = new OllamaAnalysisClient(CreateOptions(maxResponseBytes: 512), new HttpClient(handler));
      await client.EnsureModelIsPresentAsync(CancellationToken.None);

      AnalysisResult result = await client.AnalyseAsync(CreateRequest(), CancellationToken.None);

      Assert.False(result.IsAvailable);
      Assert.Null(result.Opinion);
      Assert.Contains("bound", result.FailureReason);
    }

    [Fact]
    public async Task Analyse_WhenTheEngineDoesNotAnswerInTime_IsUnavailable()
    {
      StubHttpMessageHandler handler = StubHttpMessageHandler.Respond(request => request.RequestUri.AbsolutePath == "/api/tags"
        ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(TagsBody(Model)) }
        : throw new TaskCanceledException("timeout"));

      OllamaAnalysisClient client = await CreateReadyClientAsync(handler);

      AnalysisResult result = await client.AnalyseAsync(CreateRequest(), CancellationToken.None);

      Assert.False(result.IsAvailable);
      Assert.Null(result.Opinion);
      Assert.Contains("timeout", result.FailureReason);
    }

    [Fact]
    public async Task Analyse_WhenTheEngineIsUnreachable_IsUnavailable()
    {
      StubHttpMessageHandler handler = StubHttpMessageHandler.Respond(request => request.RequestUri.AbsolutePath == "/api/tags"
        ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(TagsBody(Model)) }
        : throw new HttpRequestException("connection refused"));

      OllamaAnalysisClient client = await CreateReadyClientAsync(handler);

      AnalysisResult result = await client.AnalyseAsync(CreateRequest(), CancellationToken.None);

      Assert.False(result.IsAvailable);
      Assert.Null(result.Opinion);
      Assert.Contains("Transport failure", result.FailureReason);
    }

    [Fact]
    public async Task Analyse_WhenTheEngineAnswersWithAnErrorStatus_IsUnavailable()
    {
      StubHttpMessageHandler handler = StubHttpMessageHandler.Respond(request => request.RequestUri.AbsolutePath == "/api/tags"
        ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(TagsBody(Model)) }
        : new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("boom") });

      OllamaAnalysisClient client = await CreateReadyClientAsync(handler);

      AnalysisResult result = await client.AnalyseAsync(CreateRequest(), CancellationToken.None);

      Assert.False(result.IsAvailable);
      Assert.Null(result.Opinion);
      Assert.Contains("500", result.FailureReason);
    }

    [Fact]
    public async Task EnsureModelIsPresent_WhenTheModelIsInstalled_Succeeds()
    {
      StubHttpMessageHandler handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, TagsBody("llama3.2:1b", Model));
      OllamaAnalysisClient client = new OllamaAnalysisClient(CreateOptions(), new HttpClient(handler));

      await client.EnsureModelIsPresentAsync(CancellationToken.None);

      Assert.True(client.IsAvailable);
    }

    [Fact]
    public async Task EnsureModelIsPresent_WhenTheModelIsMissing_RefusesToBecomeUsable()
    {
      StubHttpMessageHandler handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, TagsBody("llama3.2:1b"));
      OllamaAnalysisClient client = new OllamaAnalysisClient(CreateOptions(), new HttpClient(handler));

      InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.EnsureModelIsPresentAsync(CancellationToken.None));

      Assert.Contains(Model, error.Message);
      Assert.False(client.IsAvailable);
    }

    [Fact]
    public async Task EnsureModelIsPresent_WhenTheEngineIsNotRunning_RefusesToBecomeUsable()
    {
      StubHttpMessageHandler handler = StubHttpMessageHandler.Throw(new HttpRequestException("connection refused"));
      OllamaAnalysisClient client = new OllamaAnalysisClient(CreateOptions(), new HttpClient(handler));

      InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.EnsureModelIsPresentAsync(CancellationToken.None));

      Assert.Contains("not reachable", error.Message);
      Assert.False(client.IsAvailable);
    }

    [Fact]
    public async Task Analyse_WhenNoModelIsConfigured_IsUnavailableWithNoOpinion()
    {
      UnavailableAnalysisClient client = new UnavailableAnalysisClient();

      AnalysisResult result = await client.AnalyseAsync(CreateRequest(), CancellationToken.None);

      // The supported way to run without analysis: explicitly unavailable, never a canned or derived opinion.
      Assert.False(result.IsAvailable);
      Assert.Null(result.Opinion);
      Assert.False(string.IsNullOrWhiteSpace(result.FailureReason));
      Assert.False(client.IsAvailable);
    }

    [Fact]
    public async Task Analyse_WhenTheModelWasNeverConfirmedPresent_IsUnavailableWithoutCallingTheEngine()
    {
      StubHttpMessageHandler handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, Envelope("{\"symbol\":\"EURUSD\",\"bias\":\"Bullish\",\"confidence\":0.8,\"rationale\":\"Unchecked.\"}"));
      OllamaAnalysisClient client = new OllamaAnalysisClient(CreateOptions(), new HttpClient(handler));

      AnalysisResult result = await client.AnalyseAsync(CreateRequest(), CancellationToken.None);

      Assert.False(result.IsAvailable);
      Assert.Null(result.Opinion);
      Assert.Empty(handler.Requests);
    }
  }
}
