using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Broker
{
  /// <summary>
  /// Read-only rule: an imported token pair must be present and plausible. Whether the pair is actually
  /// accepted is decided by the provider, not here.
  /// </summary>
  public class ValidateBrokerTokenImport : IRequest<bool>
  {
    public string AccessToken { get; set; }

    public string RefreshToken { get; set; }
  }
}
