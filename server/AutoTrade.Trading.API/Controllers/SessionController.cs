using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Command.Session;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Core.Query.Session;
using Hikyaku;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoTrade.Trading.API.Controllers
{
  [ApiController]
  [Route("api/session")]
  public class SessionController(IHikyaku hikyaku) : ControllerBase
  {
    public const string SessionTokenClaimType = "session_token";


    [HttpPost("login")]
    [AllowAnonymous]
    
    [ProducesResponseType(typeof(AuthenticatedSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status423Locked)]
    public async Task<IActionResult> Login([FromBody] LoginOperator command, CancellationToken cancellationToken)
    {
      LoginOperatorResult result = await hikyaku.Send(command, cancellationToken);

      if (result.Outcome == LoginOutcome.Success)
      {
        SessionDto session = await hikyaku.Send(new GetCurrentSession
        {
          OperatorId = result.OperatorId,
          SessionToken = result.SessionToken
        }, cancellationToken);

        return Ok(new AuthenticatedSessionDto
        {
          AccessToken = result.SessionToken,
          Session = session
        });
      }

      if (result.Outcome == LoginOutcome.LockedOut)
      {
        return StatusCode(StatusCodes.Status423Locked);
      }

      if (result.Outcome == LoginOutcome.AccountInactive)
      {
        return StatusCode(StatusCodes.Status403Forbidden);
      }

      return Unauthorized();
    }

    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(SessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken)
    {
      SessionDto session = await hikyaku.Send(new GetCurrentSession
      {
        OperatorId = ReadOperatorId(),
        SessionToken = ReadSessionToken()
      }, cancellationToken);

      if (session == null)
      {
        return Unauthorized();
      }

      return Ok(session);
    }

    [HttpPost("logout")]
    [Authorize]
    
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
      await hikyaku.Send(new LogoutOperator
      {
        OperatorId = ReadOperatorId(),
        SessionToken = ReadSessionToken()
      }, cancellationToken);

      return NoContent();
    }

    private Guid ReadOperatorId()
    {
      Claim claim = User.FindFirst(ClaimTypes.NameIdentifier);

      return claim != null && Guid.TryParse(claim.Value, out Guid operatorId) ? operatorId : Guid.Empty;
    }

    private string ReadSessionToken()
    {
      Claim claim = User.FindFirst(SessionTokenClaimType);

      return claim != null ? claim.Value : null;
    }
  }
}
