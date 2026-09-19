using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Handlers.Broker;
using AutoTrade.Trading.Handlers.Broker.OAuth;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.Broker
{
  /// <summary>
  /// Covers the provider transport: the HTTP method and parameters of each grant, and how a successful,
  /// rejected or unreadable response is classified. No real network call is made.
  /// </summary>
  public class BrokerTokenClientTests
  {
    private static BrokerOptions CreateOptions()
    {
      return new BrokerOptions
      {
        ClientId = "client-id",
        ClientSecret = "client-secret",
        TokenKey = Convert.ToBase64String(new byte[32]),
        RedirectUri = "http://127.0.0.1:5271/api/broker/callback",
        Environment = Core.Enums.TradingEnvironment.Demo,
        TokenEndpoint = "https://openapi.ctrader.com/apps/token"
      };
    }

    private static BrokerTokenClient CreateClient(StubHttpMessageHandler handler)
    {
      return new BrokerTokenClient(CreateOptions(), new HttpClient(handler));
    }

    private const string SuccessBody = "{\"accessToken\":\"at-123\",\"tokenType\":\"bearer\",\"expiresIn\":2628000,\"refreshToken\":\"rt-456\",\"errorCode\":null,\"description\":null}";

    [Fact]
    public async Task ExchangeAuthorizationCode_UsesGetAndSendsTheDocumentedParameters()
    {
      StubHttpMessageHandler handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, SuccessBody);
      BrokerTokenClient client = CreateClient(handler);

      BrokerTokenExchangeResult result = await client.ExchangeAuthorizationCodeAsync("auth-code", CancellationToken.None);

      Assert.True(result.IsSuccess);
      Assert.Equal("at-123", result.Tokens.AccessToken);
      Assert.Equal("rt-456", result.Tokens.RefreshToken);
      Assert.Equal(2628000, result.Tokens.ExpiresIn);

      HttpRequestMessage request = Assert.Single(handler.Requests);
      Assert.Equal(HttpMethod.Get, request.Method);

      Dictionary<string, string> query = ParseQuery(request.RequestUri.Query);
      Assert.Equal("authorization_code", query["grant_type"]);
      Assert.Equal("auth-code", query["code"]);
      Assert.Equal("client-id", query["client_id"]);
      Assert.Equal("client-secret", query["client_secret"]);
      Assert.Equal("http://127.0.0.1:5271/api/broker/callback", query["redirect_uri"]);
    }

    [Fact]
    public async Task Refresh_UsesPostAndCarriesNoRedirectUri()
    {
      StubHttpMessageHandler handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, SuccessBody);
      BrokerTokenClient client = CreateClient(handler);

      BrokerTokenExchangeResult result = await client.RefreshAsync("rt-456", CancellationToken.None);

      Assert.True(result.IsSuccess);

      HttpRequestMessage request = Assert.Single(handler.Requests);
      Assert.Equal(HttpMethod.Post, request.Method);

      Dictionary<string, string> query = ParseQuery(request.RequestUri.Query);
      Assert.Equal("refresh_token", query["grant_type"]);
      Assert.Equal("rt-456", query["refresh_token"]);
      Assert.False(query.ContainsKey("redirect_uri"));
    }

    [Fact]
    public async Task Exchange_WhenTheProviderReportsAnErrorCode_ReturnsTheDescription()
    {
      StubHttpMessageHandler handler = StubHttpMessageHandler.Respond(
        HttpStatusCode.OK,
        "{\"errorCode\":\"INVALID_REQUEST\",\"description\":\"The authorization code expired\"}");

      BrokerTokenExchangeResult result = await CreateClient(handler)
        .ExchangeAuthorizationCodeAsync("auth-code", CancellationToken.None);

      Assert.False(result.IsSuccess);
      Assert.Equal("The authorization code expired", result.ErrorDetail);
    }

    [Fact]
    public async Task Exchange_WhenTheResponseCarriesNoRefreshToken_IsNotTreatedAsSuccess()
    {
      StubHttpMessageHandler handler = StubHttpMessageHandler.Respond(
        HttpStatusCode.OK,
        "{\"accessToken\":\"at-123\",\"tokenType\":\"bearer\",\"expiresIn\":2628000,\"refreshToken\":null}");

      BrokerTokenExchangeResult result = await CreateClient(handler)
        .ExchangeAuthorizationCodeAsync("auth-code", CancellationToken.None);

      // Without a refresh token the grant cannot be renewed, so it must not be stored as valid.
      Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Exchange_WhenTheBodyIsNotJson_ReportsAnUnreadableResponseWithoutThrowing()
    {
      StubHttpMessageHandler handler = StubHttpMessageHandler.Respond(HttpStatusCode.BadGateway, "<html>gateway error</html>");

      BrokerTokenExchangeResult result = await CreateClient(handler)
        .ExchangeAuthorizationCodeAsync("auth-code", CancellationToken.None);

      Assert.False(result.IsSuccess);
      Assert.Contains("unreadable", result.ErrorDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Exchange_WhenTheTransportFails_ReportsATransportFailure()
    {
      StubHttpMessageHandler handler = StubHttpMessageHandler.Throw(new HttpRequestException("connection refused"));

      BrokerTokenExchangeResult result = await CreateClient(handler)
        .ExchangeAuthorizationCodeAsync("auth-code", CancellationToken.None);

      Assert.False(result.IsSuccess);
      Assert.Contains("connection refused", result.ErrorDetail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Exchange_WhenTheRequestIsCancelled_ReportsATimeoutWithoutThrowing()
    {
      StubHttpMessageHandler handler = StubHttpMessageHandler.Throw(new TaskCanceledException("timeout"));

      BrokerTokenExchangeResult result = await CreateClient(handler)
        .ExchangeAuthorizationCodeAsync("auth-code", CancellationToken.None);

      Assert.False(result.IsSuccess);
      Assert.Contains("did not answer", result.ErrorDetail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Exchange_EscapesValuesThatWouldBreakTheQueryString()
    {
      StubHttpMessageHandler handler = StubHttpMessageHandler.Respond(HttpStatusCode.OK, SuccessBody);
      BrokerOptions options = CreateOptions();
      options.RedirectUri = "http://127.0.0.1:5271/api/broker/callback?a=b&c=d";

      BrokerTokenClient client = new BrokerTokenClient(options, new HttpClient(handler));

      await client.ExchangeAuthorizationCodeAsync("code+with special", CancellationToken.None);

      Dictionary<string, string> query = ParseQuery(handler.Requests.Single().RequestUri.Query);
      Assert.Equal("http://127.0.0.1:5271/api/broker/callback?a=b&c=d", query["redirect_uri"]);
      Assert.Equal("code+with special", query["code"]);
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
      Dictionary<string, string> parsed = new Dictionary<string, string>();

      foreach (string pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
      {
        string[] parts = pair.Split('=', 2);
        parsed[Uri.UnescapeDataString(parts[0])] = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
      }

      return parsed;
    }

  }
}
