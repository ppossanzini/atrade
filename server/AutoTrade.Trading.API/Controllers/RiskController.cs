using System;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Query.Risk;
using Hikyaku;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoTrade.Trading.API.Controllers
{
    [ApiController]
    [Route("api/risk")]
    [Authorize]
    public class RiskController(IHikyaku hikyaku) : ControllerBase
    {
        /// <summary>
        /// Configured thresholds, including which ones are still missing. The client needs this to explain a
        /// blocking gate whose cause is configuration rather than market data.
        /// </summary>
        [HttpGet("limits")]
        [ProducesResponseType(typeof(RiskLimitsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetLimits(CancellationToken cancellationToken)
        {
            RiskLimitsDto limits = await hikyaku.Send(new GetRiskLimits(), cancellationToken);

            return Ok(limits);
        }

        [HttpGet("baskets/{basketId:guid}")]
        [ProducesResponseType(typeof(RiskDecisionDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetBasketDecision(Guid basketId, CancellationToken cancellationToken)
        {
            RiskDecisionDto decision = await hikyaku.Send(new GetBasketRiskDecision
            {
                BasketId = basketId
            }, cancellationToken);

            return decision == null ? NotFound() : Ok(decision);
        }
    }
}
