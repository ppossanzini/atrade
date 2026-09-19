using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AutoTrade.Trading.Handlers.MarketData
{
  /// <summary>
  /// The source in force when no provider is configured. It never returns data, and that is a supported
  /// state: the application runs, the gates block, and nothing pretends a market was observed.
  /// </summary>
  public class UnavailableMarketDataSource : IMarketDataSource
  {
    public Task<MarketDataCapture> CaptureAsync(IReadOnlyList<SymbolRequest> symbols, CancellationToken cancellationToken)
    {
      return Task.FromResult(MarketDataCapture.Unavailable());
    }

    public Task<List<SymbolSpecification>> DescribeAsync(IReadOnlyList<SymbolRequest> symbols, CancellationToken cancellationToken)
    {
      return Task.FromResult(new List<SymbolSpecification>());
    }
  }
}
