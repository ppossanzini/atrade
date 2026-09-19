using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AutoTrade.Trading.Handlers.Tests
{
  /// <summary>Records the requests it receives and answers with a scripted response.</summary>
  internal sealed class StubHttpMessageHandler : HttpMessageHandler
  {
    private readonly Func<HttpRequestMessage, HttpResponseMessage> responseFactory;

    private StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
      this.responseFactory = responseFactory;
    }

    public List<HttpRequestMessage> Requests { get; } = new List<HttpRequestMessage>();

    /// <summary>
    /// Bodies copied while the request is still alive: the caller owns and disposes its own request, so reading
    /// the content afterwards would fail on a disposed object.
    /// </summary>
    public List<string> Bodies { get; } = new List<string>();

    public static StubHttpMessageHandler Respond(HttpStatusCode statusCode, string body)
    {
      return new StubHttpMessageHandler(request => new HttpResponseMessage(statusCode)
      {
        Content = new StringContent(body)
      });
    }

    public static StubHttpMessageHandler Respond(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
      return new StubHttpMessageHandler(responseFactory);
    }

    public static StubHttpMessageHandler Throw(Exception error)
    {
      return new StubHttpMessageHandler(request => throw error);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Requests.Add(request);
      Bodies.Add(request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken));

      return responseFactory(request);
    }
  }
}
