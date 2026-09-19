namespace AutoTrade.Trading.Core.Enums
{
  /// <summary>
  /// Declared entry rule of a strategy version. It belongs to the version like every other rule, so a
  /// proposal can always say which rule the strategy was following when it was produced.
  ///
  /// The modes are declarations. Which of them can actually be exercised depends on the evidence a proposal
  /// source has: the deterministic source of this slice observes one capture and therefore cannot measure
  /// momentum or a regime, so it records the declared mode and leaves the verdict to the risk gate. The
  /// evidence driven source that will exercise them arrives with the RAG slice, and it plugs in behind the
  /// same contract without changing this vocabulary.
  /// </summary>
  public enum EntryMode
  {
    /// <summary>Momentum, admitted only while the market regime agrees.</summary>
    RegimeMomentum = 0,

    /// <summary>Momentum alone.</summary>
    Momentum = 1,

    /// <summary>Reversion against the prevailing move.</summary>
    MeanReversion = 2
  }
}
