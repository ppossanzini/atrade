using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Command.Market;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Core.Query.Market;
using AutoTrade.Trading.Core.Query.Operations;
using Hikyaku;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoTrade.Trading.API.Controllers
{
  /// <summary>
  /// Market Manager surface: the operating mode, the analysis switch, the proposal queue and the operator
  /// decisions. Every mutating endpoint demands the antiforgery token and carries the operator identity,
  /// because a decision is attributed to a person and not to the process.
  /// </summary>
  [ApiController]
  [Route("api/market")]
  [Authorize]
  public class MarketController(IHikyaku hikyaku) : ControllerBase
  {
    [HttpGet("manager")]
    [ProducesResponseType(typeof(MarketManagerStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetManager(CancellationToken cancellationToken)
    {
      OperationalStatusDto status = await hikyaku.Send(new GetOperationalStatus(), cancellationToken);

      return Ok(status.MarketManager);
    }

    [HttpPut("manager/mode")]
    
    [ProducesResponseType(typeof(MarketManagerMode), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetMode([FromBody] MarketManagerModeChangeDto request, CancellationToken cancellationToken)
    {
      if (!Enum.TryParse(request.Mode, true, out MarketManagerMode mode))
      {
        return BadRequest(new ProblemDetails { Title = "Unknown market manager mode." });
      }

      MarketManagerMode applied = await hikyaku.Send(new SetMarketManagerMode
      {
        Mode = mode,
        OperatorId = ReadOperatorId()
      }, cancellationToken);

      return Ok(applied);
    }

    [HttpPut("manager/analysis")]
    
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetAnalysisState([FromBody] AnalysisStateChangeDto request, CancellationToken cancellationToken)
    {
      bool applied = await hikyaku.Send(new SetAnalysisState
      {
        IsRunning = request.IsRunning,
        OperatorId = ReadOperatorId()
      }, cancellationToken);

      // A refused change is not a transport error: the cycle timing, the active version or the request itself
      // do not admit the change yet, so the caller learns it as a conflict instead of a silent success.
      return applied ? NoContent() : Conflict(new ProblemDetails { Title = "The analysis state cannot be changed in the current conditions." });
    }

    [HttpGet("proposals")]
    [ProducesResponseType(typeof(List<ProposalSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProposals(CancellationToken cancellationToken)
    {
      List<ProposalSummaryDto> queue = await hikyaku.Send(new GetProposalQueue(), cancellationToken);

      return Ok(queue);
    }

    [HttpGet("proposals/{proposalId:guid}")]
    [ProducesResponseType(typeof(ProposalDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProposal(Guid proposalId, CancellationToken cancellationToken)
    {
      ProposalDetailDto proposal = await hikyaku.Send(new GetProposalDetail { ProposalId = proposalId }, cancellationToken);

      return proposal == null ? NotFound() : Ok(proposal);
    }

    [HttpPost("proposals/{proposalId:guid}/approve")]
    
    [ProducesResponseType(typeof(ProposalDecisionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Approve(Guid proposalId, CancellationToken cancellationToken)
    {
      return DecideAsync(new ApproveProposal
      {
        ProposalId = proposalId,
        OperatorId = ReadOperatorId()
      }, cancellationToken);
    }

    [HttpPost("proposals/{proposalId:guid}/reject")]
    
    [ProducesResponseType(typeof(ProposalDecisionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Reject(Guid proposalId, [FromBody] ProposalDecisionDto request, CancellationToken cancellationToken)
    {
      if (string.IsNullOrWhiteSpace(request != null ? request.Reason : null))
      {
        return Task.FromResult<IActionResult>(BadRequest(new ProblemDetails { Title = "A rejection requires a reason." }));
      }

      return DecideAsync(new RejectProposal
      {
        ProposalId = proposalId,
        OperatorId = ReadOperatorId(),
        Reason = request.Reason.Trim()
      }, cancellationToken);
    }

    [HttpPost("proposals/{proposalId:guid}/suspend")]
    
    [ProducesResponseType(typeof(ProposalDecisionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Suspend(Guid proposalId, [FromBody] ProposalDecisionDto request, CancellationToken cancellationToken)
    {
      if (string.IsNullOrWhiteSpace(request != null ? request.Reason : null))
      {
        return Task.FromResult<IActionResult>(BadRequest(new ProblemDetails { Title = "A suspension requires a reason." }));
      }

      return DecideAsync(new SuspendProposal
      {
        ProposalId = proposalId,
        OperatorId = ReadOperatorId(),
        Reason = request.Reason.Trim()
      }, cancellationToken);
    }

    private async Task<IActionResult> DecideAsync<TRequest>(TRequest request, CancellationToken cancellationToken)
      where TRequest : IRequest<ProposalDecisionResultDto>
    {
      ProposalDecisionResultDto result = await hikyaku.Send(request, cancellationToken);

      if (result.Outcome == ProposalDecisionOutcome.NotFound)
      {
        return NotFound(result);
      }

      if (result.Outcome != ProposalDecisionOutcome.Applied)
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
