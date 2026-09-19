namespace AutoTrade.Trading.Core.Enums
{
  /// <summary>
  /// What a proposal asks for. The vocabulary is part of the audit contract: a proposal that asked to
  /// exit must never be readable as a proposal that asked to enter.
  /// </summary>
  public enum ProposalAction
  {
    Entry = 0,
    Reduce = 1,
    Exit = 2
  }
}
