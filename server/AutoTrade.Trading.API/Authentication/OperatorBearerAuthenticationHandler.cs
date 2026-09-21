using System.Security.Claims;
using System.Text.Encodings.Web;
using AutoTrade.Trading.API.Controllers;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Query.Session;
using Hikyaku;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace AutoTrade.Trading.API.Authentication
{
  public class OperatorBearerAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IHikyaku hikyaku) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
  {
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
      string authorization = Request.Headers[HeaderNames.Authorization];

      if (string.IsNullOrWhiteSpace(authorization))
      {
        return AuthenticateResult.NoResult();
      }

      const string bearerPrefix = "Bearer ";
      if (!authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
      {
        return AuthenticateResult.NoResult();
      }

      string token = authorization[bearerPrefix.Length..].Trim();
      if (string.IsNullOrWhiteSpace(token))
      {
        return AuthenticateResult.Fail("The bearer token is missing.");
      }

      SessionDto session = await hikyaku.Send(new GetCurrentSessionByToken
      {
        SessionToken = token
      }, Context.RequestAborted);

      if (session == null)
      {
        return AuthenticateResult.Fail("The bearer token is invalid or expired.");
      }

      Claim[] claims =
      {
        new Claim(ClaimTypes.NameIdentifier, session.OperatorId.ToString()),
        new Claim(ClaimTypes.Name, session.UserName),
        new Claim(SessionController.SessionTokenClaimType, token)
      };

      ClaimsIdentity identity = new ClaimsIdentity(claims, Scheme.Name);
      AuthenticationTicket ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);

      return AuthenticateResult.Success(ticket);
    }
  }
}
