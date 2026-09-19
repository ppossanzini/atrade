using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Query.Operations;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.Operations
{
  /// <summary>
  /// The operational status has to answer for the semantic memory as well. Without a store configured the
  /// answer is "jigen/none is not available", which is a reported state: an operator reading the status must
  /// never have to infer from an empty retrieval that semantic memory is off.
  /// </summary>
  public class OperationalStatusEvidenceTests
  {
    private static TradingTestContext CreateContext()
    {
      return new TradingTestContext(new Dictionary<string, string>());
    }

    [Fact]
    public async Task Status_WithoutAConfiguredStore_ReportsItUnavailable()
    {
      using TradingTestContext context = CreateContext();

      OperationalStatusDto status = await context.Hikyaku.Send(new GetOperationalStatus(), CancellationToken.None);

      Assert.NotNull(status.EvidenceStore);
      Assert.Equal("None", status.EvidenceStore.Provider);
      Assert.False(status.EvidenceStore.IsAvailable);
    }
  }
}
