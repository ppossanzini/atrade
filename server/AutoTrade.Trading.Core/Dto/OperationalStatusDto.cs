using System;

namespace AutoTrade.Trading.Core.Dto
{
  public class OperationalStatusDto
  {
    public DateTime ServerTimeUtc { get; set; }
    public KillSwitchStatusDto KillSwitch { get; set; }
    public TradingAccountStatusDto Account { get; set; }
    public MarketManagerStatusDto MarketManager { get; set; }

    /// <summary>Semantic memory in force, so its absence is a reported state and not a silent one.</summary>
    public EvidenceStoreStatusDto EvidenceStore { get; set; }

    /// <summary>
    /// How text is turned into vectors. Reported next to the store because the two are separate facts: a store
    /// that is open with no embedding source cannot answer a retrieval, and that has to be visible.
    /// </summary>
    public EmbeddingSourceStatusDto Embedding { get; set; }

    /// <summary>Analysis model in force, reported for the same reason as the semantic memory.</summary>
    public AnalysisModelStatusDto AnalysisModel { get; set; }
  }
}
