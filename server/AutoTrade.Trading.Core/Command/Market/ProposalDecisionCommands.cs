using System;
using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Market
{
  /// <summary>
  /// Approves a proposal under review. The gate is re-evaluated when the decision is applied, so an
  /// approval can never forward a proposal whose risk has changed since it was generated.
  /// </summary>
  public class ApproveProposal : IRequest<ProposalDecisionResultDto>
  {
    public Guid ProposalId { get; set; }

    public Guid OperatorId { get; set; }
  }

  /// <summary>Rejects a proposal. A reason is mandatory: a refusal without a reason is not auditable.</summary>
  public class RejectProposal : IRequest<ProposalDecisionResultDto>
  {
    public Guid ProposalId { get; set; }

    public Guid OperatorId { get; set; }

    public string Reason { get; set; }
  }

  /// <summary>
  /// Suspends a proposal. The proposal stays visible and is not forwarded; suspension is not a rejection
  /// and is not a permission either.
  /// </summary>
  public class SuspendProposal : IRequest<ProposalDecisionResultDto>
  {
    public Guid ProposalId { get; set; }

    public Guid OperatorId { get; set; }

    public string Reason { get; set; }
  }
}
