namespace AutoTrade.Trading.Core.Dto
{
  /// <summary>
  /// Risk settings that live in deployment configuration, surfaced so the operator can see what is actually
  /// in force. Only the snapshot validity window remains here: the leg limits belong to the leg and are read
  /// from the basket version, so they are reported with the basket and not with the deployment.
  /// </summary>
  public class RiskLimitsDto
  {
    public int? SnapshotMaxAgeSeconds { get; set; }

    public bool IsConfigured { get; set; }
  }
}
