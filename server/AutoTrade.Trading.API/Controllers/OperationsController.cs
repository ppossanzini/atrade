using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Command.Operations;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Core.Query.Operations;
using Hikyaku;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoTrade.Trading.API.Controllers
{
  [ApiController]
  [Route("api/operations")]
  [Authorize]
  public class OperationsController(IHikyaku hikyaku) : ControllerBase
  {
    [HttpGet("status")]
    [ProducesResponseType(typeof(OperationalStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
      OperationalStatusDto status = await hikyaku.Send(new GetOperationalStatus(), cancellationToken);

      return Ok(status);
    }

    [HttpGet("promotion")]
    [ProducesResponseType(typeof(PromotionStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPromotion(CancellationToken cancellationToken)
    {
      PromotionStatusDto status = await hikyaku.Send(new GetPromotionStatus(), cancellationToken);

      return Ok(status);
    }

    [HttpPost("kill-switch/engage")]
    [ValidateAntiForgeryToken]
  [ProducesResponseType(typeof(KillSwitchChangeResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> EngageKillSwitch([FromBody] KillSwitchEngagementDto request, CancellationToken cancellationToken)
    {
      KillSwitchChangeResult result = await hikyaku.Send(new EngageKillSwitch
      {
        OperatorId = ReadOperatorId(),
        Reason = request.Reason
      }, cancellationToken);

      return Ok(result);
    }

    [HttpPost("kill-switch/release")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(typeof(KillSwitchChangeResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReleaseKillSwitch(CancellationToken cancellationToken)
    {
      KillSwitchChangeResult result = await hikyaku.Send(new ReleaseKillSwitch
      {
        OperatorId = ReadOperatorId()
      }, cancellationToken);

      if (result.Outcome == KillSwitchChangeOutcome.Blocked)
      {
        return Conflict(result);
      }

      return Ok(result);
    }

    private Guid ReadOperatorId()
    {
      Claim claim = User.FindFirst(ClaimTypes.NameIdentifier);

      return claim != null && Guid.TryParse(claim.Value, out Guid operatorId) ? operatorId : Guid.Empty;
    }
  }
}
