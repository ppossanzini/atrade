using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Handlers.Broker.Protocol;

namespace AutoTrade.Trading.Handlers.MarketData
{
  /// <summary>
  /// cTrader realization of the market-data seam. Provider facts are mapped here once: no risk or execution
  /// handler needs to know about protobuf scales, symbol IDs, or the broker's position model.
  /// </summary>
  public sealed class CtraderMarketDataSource(ICtraderSnapshotReader snapshotReader) : IMarketDataSource
  {
    public async Task<MarketDataCapture> CaptureAsync(IReadOnlyList<SymbolRequest> symbols, CancellationToken cancellationToken)
    {
      CtraderSnapshotReadResult snapshot = await snapshotReader.ReadAsync(symbols, cancellationToken);

      if (!snapshot.IsSuccess)
      {
        return MarketDataCapture.Unavailable();
      }

      Dictionary<long, ProtoOASymbol> details = snapshot.SymbolDetails.ToDictionary(item => item.SymbolId);
      Dictionary<long, ProtoOASpotEvent> spots = snapshot.Spots.ToDictionary(item => item.SymbolId);
      Dictionary<long, ProtoOALightSymbol> lightSymbols = snapshot.Symbols.ToDictionary(item => item.SymbolId);
      int moneyDigits = snapshot.Trader != null && snapshot.Trader.MoneyDigits > 0
        ? (int)snapshot.Trader.MoneyDigits
        : (int)snapshot.MoneyDigits;
      double scale = Math.Pow(10, moneyDigits);
      double balance = snapshot.Trader != null ? snapshot.Trader.Balance / scale : 0;
      double unrealized = snapshot.UnrealizedPnls.Sum(item => item.NetUnrealizedPnL / Math.Pow(10, snapshot.MoneyDigits));
      Dictionary<long, string> assets = snapshot.Assets.ToDictionary(item => item.AssetId, item => item.Name);

      AccountCapture account = new AccountCapture
      {
        Balance = balance,
        Equity = balance + unrealized,
        UnrealizedPnl = unrealized,
        RealizedPnlToday = RealizedPnl(snapshot),
        Environment = snapshot.Environment,
        Currency = snapshot.Trader != null && assets.TryGetValue(snapshot.Trader.DepositAssetId, out string currency) ? currency : null
      };

      List<SymbolCapture> captures = new List<SymbolCapture>();
      if (symbols != null)
      {
        foreach (SymbolRequest request in symbols)
        {
          if (request == null || string.IsNullOrWhiteSpace(request.Symbol))
          {
            continue;
          }

          ProtoOALightSymbol light = lightSymbols.Values.FirstOrDefault(item => string.Equals(item.SymbolName, request.Symbol, StringComparison.OrdinalIgnoreCase));
          ProtoOASymbol detail = light != null && details.TryGetValue(light.SymbolId, out ProtoOASymbol found) ? found : null;
          ProtoOASpotEvent spot = light != null && spots.TryGetValue(light.SymbolId, out ProtoOASpotEvent quote) ? quote : null;
          double? bid = spot != null && spot.HasBid ? spot.Bid / 100000d : (double?)null;
          double? ask = spot != null && spot.HasAsk ? spot.Ask / 100000d : (double?)null;
          double? spread = bid.HasValue && ask.HasValue && detail != null && detail.PipPosition >= 0
            ? (ask.Value - bid.Value) / Math.Pow(10, -detail.PipPosition)
            : (double?)null;
          double? volatility = light != null && snapshot.TrendbarsBySymbolId.TryGetValue(light.SymbolId, out List<ProtoOATrendbar> trendbars)
            ? CtraderVolatilityCalculator.Calculate(trendbars, snapshot.CapturedAtUtc)
            : (double?)null;

          captures.Add(new SymbolCapture
          {
            Symbol = request.Symbol,
            Price = bid.HasValue && ask.HasValue ? (bid.Value + ask.Value) / 2 : bid ?? ask,
            SpreadPips = spread,
            VolatilityPercent = volatility,
            IsTradable = detail != null && spot != null && detail.TradingMode == ProtoOATradingMode.Enabled
          });
        }
      }

      return new MarketDataCapture
      {
        IsAvailable = true,
        CapturedAtUtc = snapshot.CapturedAtUtc,
        Account = account,
        Symbols = captures
      };
    }

    public async Task<List<SymbolSpecification>> DescribeAsync(IReadOnlyList<SymbolRequest> symbols, CancellationToken cancellationToken)
    {
      CtraderSnapshotReadResult snapshot = await snapshotReader.ReadAsync(symbols, cancellationToken);
      if (!snapshot.IsSuccess)
      {
        return new List<SymbolSpecification>();
      }

      Dictionary<long, ProtoOASymbol> details = snapshot.SymbolDetails.ToDictionary(item => item.SymbolId);
      Dictionary<long, ProtoOALightSymbol> lightSymbols = snapshot.Symbols.ToDictionary(item => item.SymbolId);
      Dictionary<long, string> assets = snapshot.Assets.ToDictionary(item => item.AssetId, item => item.Name);
      List<SymbolSpecification> specifications = new List<SymbolSpecification>();

      if (symbols == null)
      {
        return specifications;
      }

      foreach (SymbolRequest request in symbols)
      {
        if (request == null || string.IsNullOrWhiteSpace(request.Symbol))
        {
          continue;
        }

        ProtoOALightSymbol light = lightSymbols.Values.FirstOrDefault(item => string.Equals(item.SymbolName, request.Symbol, StringComparison.OrdinalIgnoreCase));
        ProtoOASymbol detail = light != null && details.TryGetValue(light.SymbolId, out ProtoOASymbol found) ? found : null;

        if (detail == null)
        {
          continue;
        }

        specifications.Add(new SymbolSpecification
        {
          Symbol = request.Symbol,
          Market = request.Market,
          MinVolume = ToInt(detail.MinVolume),
          StepVolume = ToInt(detail.StepVolume),
          MaxVolume = ToInt(detail.MaxVolume),
          LotSize = ToInt(detail.LotSize),
          PipSizePerUnit = detail.PipPosition >= 0 ? Math.Pow(10, -detail.PipPosition) : 0,
          IsTradable = detail.TradingMode == ProtoOATradingMode.Enabled,
          ProfitCurrency = light != null && assets.TryGetValue(light.QuoteAssetId, out string quoteCurrency) ? quoteCurrency : null
        });
      }

      return specifications;
    }

    private static double RealizedPnl(CtraderSnapshotReadResult snapshot)
    {
      double total = 0;

      foreach (ProtoOADeal deal in snapshot.DealsToday)
      {
        if (deal.ClosePositionDetail == null)
        {
          continue;
        }

        double scale = Math.Pow(10, deal.HasMoneyDigits ? deal.MoneyDigits : snapshot.MoneyDigits);
        ProtoOAClosePositionDetail close = deal.ClosePositionDetail;
        total += (close.GrossProfit + close.Swap + close.Commission) / scale;
      }

      return total;
    }

    private static int ToInt(long value)
    {
      return value <= 0 ? 0 : value > int.MaxValue ? int.MaxValue : (int)value;
    }
  }
}
