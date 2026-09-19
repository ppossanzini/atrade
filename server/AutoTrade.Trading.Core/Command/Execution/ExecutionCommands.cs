using System;
using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Execution
{
  /// <summary>
  /// Turns an approved proposal into a persisted execution and starts sending its legs, one at a time.
  /// The execution is written before anything is sent, so a failure in the middle never loses what was
  /// already decided.
  /// </summary>
  public class StartExecution : IRequest<ExecutionStartResultDto>
  {
    public Guid ProposalId { get; set; }

    public Guid OperatorId { get; set; }
  }

  /// <summary>
  /// Confirms the compensation of a partially executed basket. It is a new sequence of orders, never a
  /// rollback, and it is always an explicit operator act.
  /// </summary>
  public class ConfirmCompensation : IRequest<ExecutionStartResultDto>
  {
    public Guid ExecutionId { get; set; }

    public Guid OperatorId { get; set; }

    public string Reason { get; set; }
  }

  /// <summary>Validation type: whether a proposal may produce an execution at all.</summary>
  public class ValidateExecutionStartable : IRequest<bool>
  {
    public Guid ProposalId { get; set; }
  }

  /// <summary>Validation type: whether an execution is in the state that admits a compensation.</summary>
  public class ValidateCompensationConfirmable : IRequest<bool>
  {
    public Guid ExecutionId { get; set; }
  }
}
