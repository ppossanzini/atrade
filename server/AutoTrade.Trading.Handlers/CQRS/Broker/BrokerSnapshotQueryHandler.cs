using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Query.Broker;
using AutoTrade.Trading.Handlers.Broker.Protocol;
using Hikyaku;

namespace AutoTrade.Trading.Handlers.CQRS.Broker
{
  public sealed class BrokerSnapshotQueryHandler(ICtraderSnapshotReader snapshotReader) : IRequestHandler<GetBrokerSnapshot, BrokerSnapshotDto>
  {
    public async Task<BrokerSnapshotDto> Handle(GetBrokerSnapshot request, CancellationToken cancellationToken)
    {
      CtraderSnapshotReadResult snapshot = await snapshotReader.ReadAsync(null, cancellationToken);
      BrokerSnapshotDto result = new BrokerSnapshotDto
      {
        IsAvailable = snapshot.IsSuccess,
        ErrorCode = snapshot.ErrorCode,
        Description = snapshot.Description,
        CapturedAtUtc = snapshot.CapturedAtUtc,
        CtidTraderAccountId = snapshot.CtidTraderAccountId,
        Environment = snapshot.Environment,
        Symbols = new List<BrokerSymbolSnapshotDto>(),
        Positions = new List<BrokerPositionSnapshotDto>(),
        PendingOrders = new List<BrokerPendingOrderSnapshotDto>()
      };

      if (!snapshot.IsSuccess)
      {
        return result;
      }

      Dictionary<long, ProtoOALightSymbol> symbolsById = snapshot.Symbols.ToDictionary(item => item.SymbolId);
      Dictionary<long, ProtoOASymbol> detailsById = snapshot.SymbolDetails.ToDictionary(item => item.SymbolId);
      Dictionary<long, string> assetNames = snapshot.Assets.ToDictionary(item => item.AssetId, item => item.Name);
      int moneyDigits = snapshot.Trader != null && snapshot.Trader.MoneyDigits > 0 ? (int)snapshot.Trader.MoneyDigits : (int)snapshot.MoneyDigits;
      double accountScale = System.Math.Pow(10, moneyDigits);
      double unrealized = snapshot.UnrealizedPnls.Sum(item => item.NetUnrealizedPnL / System.Math.Pow(10, snapshot.MoneyDigits));
      double realized = snapshot.DealsToday
        .Where(item => item.ClosePositionDetail != null)
        .Sum(item => (item.ClosePositionDetail.GrossProfit + item.ClosePositionDetail.Swap + item.ClosePositionDetail.Commission) / System.Math.Pow(10, item.HasMoneyDigits ? item.MoneyDigits : snapshot.MoneyDigits));

      result.Account = new BrokerAccountSnapshotDto
      {
        Balance = snapshot.Trader.Balance / accountScale,
        UnrealizedPnl = unrealized,
        RealizedPnlToday = realized,
        Currency = assetNames.TryGetValue(snapshot.Trader.DepositAssetId, out string currency) ? currency : null
      };

      foreach (ProtoOALightSymbol symbol in snapshot.Symbols)
      {
        ProtoOASymbol detail = detailsById.TryGetValue(symbol.SymbolId, out ProtoOASymbol found) ? found : null;
        result.Symbols.Add(new BrokerSymbolSnapshotDto
        {
          SymbolId = symbol.SymbolId,
          Symbol = symbol.SymbolName,
          IsEnabled = symbol.Enabled,
          IsTradingEnabled = detail != null && detail.TradingMode == ProtoOATradingMode.Enabled,
          Digits = detail != null ? detail.Digits : 0,
          PipPosition = detail != null ? detail.PipPosition : 0,
          MinVolume = detail != null ? detail.MinVolume : 0,
          StepVolume = detail != null ? detail.StepVolume : 0,
          MaxVolume = detail != null ? detail.MaxVolume : 0,
          LotSize = detail != null ? detail.LotSize : 0
        });
      }

      foreach (ProtoOAPosition position in snapshot.Positions)
      {
        result.Positions.Add(new BrokerPositionSnapshotDto
        {
          PositionId = position.PositionId,
          SymbolId = position.TradeData.SymbolId,
          Symbol = SymbolName(symbolsById, position.TradeData.SymbolId),
          TradeSide = position.TradeData.TradeSide.ToString(),
          Volume = position.TradeData.Volume,
          Price = position.HasPrice ? position.Price : (double?)null,
          StopLoss = position.HasStopLoss ? position.StopLoss : (double?)null,
          TakeProfit = position.HasTakeProfit ? position.TakeProfit : (double?)null
        });
      }

      foreach (ProtoOAOrder order in snapshot.PendingOrders)
      {
        result.PendingOrders.Add(new BrokerPendingOrderSnapshotDto
        {
          OrderId = order.OrderId,
          SymbolId = order.TradeData.SymbolId,
          Symbol = SymbolName(symbolsById, order.TradeData.SymbolId),
          OrderType = order.OrderType.ToString(),
          OrderStatus = order.OrderStatus.ToString(),
          TradeSide = order.TradeData.TradeSide.ToString(),
          Volume = order.TradeData.Volume,
          LimitPrice = order.HasLimitPrice ? order.LimitPrice : (double?)null,
          StopPrice = order.HasStopPrice ? order.StopPrice : (double?)null,
          ClientOrderId = order.ClientOrderId
        });
      }

      return result;
    }

    private static string SymbolName(Dictionary<long, ProtoOALightSymbol> symbolsById, long symbolId)
    {
      return symbolsById.TryGetValue(symbolId, out ProtoOALightSymbol symbol) ? symbol.SymbolName : null;
    }
  }
}
