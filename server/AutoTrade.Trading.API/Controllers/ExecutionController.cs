using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Command.Execution;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Core.Query.Execution;
using Hikyaku;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoTrade.Trading.API.Controllers
{
    /// <summary>
    /// Execution surface. Starting an execution and confirming a compensation both send orders, so both demand
    /// an authenticated operator identity: an order is attributed to a person.
    /// </summary>
    [ApiController]
    [Route("api/execution")]
    [Authorize]
    public class ExecutionController(IHikyaku hikyaku) : ControllerBase
    {
        [HttpGet("executions")]
        [ProducesResponseType(typeof(List<ExecutionSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetExecutions(CancellationToken cancellationToken)
        {
            List<ExecutionSummaryDto> queue = await hikyaku.Send(new GetExecutionQueue(), cancellationToken);

            return Ok(queue);
        }

        [HttpGet("executions/{executionId:guid}")]
        [ProducesResponseType(typeof(ExecutionDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetExecution(Guid executionId, CancellationToken cancellationToken)
        {
            ExecutionDetailDto execution = await hikyaku.Send(new GetExecutionDetail { ExecutionId = executionId }, cancellationToken);

            return execution == null ? NotFound() : Ok(execution);
        }

        [HttpPost("proposals/{proposalId:guid}/start")]

        [ProducesResponseType(typeof(ExecutionStartResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Start(Guid proposalId, CancellationToken cancellationToken)
        {
            ExecutionStartResultDto result = await hikyaku.Send(new StartExecution
            {
                ProposalId = proposalId,
                OperatorId = ReadOperatorId()
            }, cancellationToken);

            // NotConfigured and Blocked are not transport errors: the request was understood and refused, and the
            // reason travels with it.
            return result.Outcome == ExecutionOutcome.Applied ? Ok(result) : Conflict(result);
        }

        [HttpPost("executions/{executionId:guid}/compensate")]

        [ProducesResponseType(typeof(ExecutionStartResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Compensate(Guid executionId, [FromBody] CompensationRequestDto request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request != null ? request.Reason : null))
            {
                return BadRequest(new ProblemDetails { Title = "Compensating real exposure requires a reason." });
            }

            ExecutionStartResultDto result = await hikyaku.Send(new ConfirmCompensation
            {
                ExecutionId = executionId,
                OperatorId = ReadOperatorId(),
                Reason = request.Reason.Trim()
            }, cancellationToken);

            if (result.Outcome == ExecutionOutcome.NotFound)
            {
                return NotFound(result);
            }

            return result.Outcome == ExecutionOutcome.Applied ? Ok(result) : Conflict(result);
        }

        private Guid ReadOperatorId()
        {
            Claim claim = User.FindFirst(ClaimTypes.NameIdentifier);

            return claim != null && Guid.TryParse(claim.Value, out Guid operatorId) ? operatorId : Guid.Empty;
        }
    }
}
