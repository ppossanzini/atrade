using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Handlers.Broker.OAuth;

namespace AutoTrade.Trading.Handlers.Tests.Broker
{
  /// <summary>
  /// Deterministic stand-in for the provider token endpoint. It records the tokens it was asked to
  /// exchange so tests can assert the handler passes the right grant, without touching the network.
  /// </summary>
  internal sealed class FakeBrokerTokenClient : IBrokerTokenClient
  {
    public BrokerTokenExchangeResult ExchangeResult { get; set; }

    public BrokerTokenExchangeResult RefreshResult { get; set; }

    public List<string> ExchangedCodes { get; } = new List<string>();

    public List<string> RefreshedTokens { get; } = new List<string>();

    public Task<BrokerTokenExchangeResult> ExchangeAuthorizationCodeAsync(string code, CancellationToken cancellationToken)
    {
      ExchangedCodes.Add(code);

      return Task.FromResult(ExchangeResult ?? Failure("no exchange result configured"));
    }

    public Task<BrokerTokenExchangeResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
      RefreshedTokens.Add(refreshToken);

      return Task.FromResult(RefreshResult ?? Failure("no refresh result configured"));
    }

    public static BrokerTokenExchangeResult Success(string accessToken, string refreshToken, long expiresInSeconds)
    {
      return new BrokerTokenExchangeResult
      {
        IsSuccess = true,
        Tokens = new BrokerTokenResponse
        {
          AccessToken = accessToken,
          RefreshToken = refreshToken,
          TokenType = "bearer",
          ExpiresIn = expiresInSeconds
        }
      };
    }

    public static BrokerTokenExchangeResult Failure(string detail)
    {
      return new BrokerTokenExchangeResult
      {
        IsSuccess = false,
        ErrorDetail = detail
      };
    }
  }
}
