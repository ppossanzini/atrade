using System;
using System.Collections.Generic;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Core.Dto
{
  public class BrokerSnapshotDto
  {
    public bool IsAvailable { get; set; }

    public string ErrorCode { get; set; }

    public string Description { get; set; }

    public DateTime CapturedAtUtc { get; set; }

    public long CtidTraderAccountId { get; set; }

    public TradingEnvironment Environment { get; set; }

    public BrokerAccountSnapshotDto Account { get; set; }

    public List<BrokerSymbolSnapshotDto> Symbols { get; set; }

    public List<BrokerPositionSnapshotDto> Positions { get; set; }

    public List<BrokerPendingOrderSnapshotDto> PendingOrders { get; set; }
  }

  public class BrokerAccountSnapshotDto
  {
    public double Balance { get; set; }

    public double UnrealizedPnl { get; set; }

    public double RealizedPnlToday { get; set; }

    public string Currency { get; set; }
  }

  public class BrokerSymbolSnapshotDto
  {
    public long SymbolId { get; set; }

    public string Symbol { get; set; }

    public bool IsEnabled { get; set; }

    public bool IsTradingEnabled { get; set; }

    public int Digits { get; set; }

    public int PipPosition { get; set; }

    public long MinVolume { get; set; }

    public long StepVolume { get; set; }

    public long MaxVolume { get; set; }

    public long LotSize { get; set; }
  }

  public class BrokerPositionSnapshotDto
  {
    public long PositionId { get; set; }

    public string Symbol { get; set; }

    public long SymbolId { get; set; }

    public string TradeSide { get; set; }

    public long Volume { get; set; }

    public double? Price { get; set; }

    public double? StopLoss { get; set; }

    public double? TakeProfit { get; set; }
  }

  public class BrokerPendingOrderSnapshotDto
  {
    public long OrderId { get; set; }

    public string Symbol { get; set; }

    public long SymbolId { get; set; }

    public string OrderType { get; set; }

    public string OrderStatus { get; set; }

    public string TradeSide { get; set; }

    public long Volume { get; set; }

    public double? LimitPrice { get; set; }

    public double? StopPrice { get; set; }

    public string ClientOrderId { get; set; }
  }
}
