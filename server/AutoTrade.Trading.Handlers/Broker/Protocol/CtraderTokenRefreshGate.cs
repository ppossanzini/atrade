using System.Threading;

namespace AutoTrade.Trading.Handlers.Broker.Protocol
{
  /// <summary>
  /// One process-wide refresh gate shared by read and execution sessions. cTrader rotates refresh tokens,
  /// so separate gates could spend the same token concurrently from two scoped DbContexts.
  /// </summary>
  internal static class CtraderTokenRefreshGate
  {
    public static readonly SemaphoreSlim Instance = new SemaphoreSlim(1, 1);
  }
}
