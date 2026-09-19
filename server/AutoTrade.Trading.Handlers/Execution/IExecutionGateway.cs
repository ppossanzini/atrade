using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Handlers.Execution
{
  /// <summary>One leg to send. The client order id is the idempotency key, produced before the call.</summary>
  public class OrderRequest
  {
    public string ClientOrderId { get; set; }

    public string Symbol { get; set; }

    public MarketKind Market { get; set; }

    public LegDirection Direction { get; set; }

    public int VolumeUnits { get; set; }
  }

  /// <summary>
  /// What the provider reported about an order. A partial fill carries the **incremental** volume filled by
  /// that event, not the cumulative one: the leg accumulates, so an event applied twice would double count.
  /// That is exactly what the broker event identity is here to prevent.
  /// </summary>
  public class OrderEventPayload
  {
    public string BrokerEventId { get; set; }

    public ExecutionEventKind Kind { get; set; }

    public string Symbol { get; set; }

    public int FilledVolumeUnits { get; set; }

    public double? AveragePrice { get; set; }

    public string Payload { get; set; }
  }

  /// <summary>
  /// Outcome of a send. <see cref="NoResponse"/> is not a rejection: nothing was learned, which is why the
  /// caller must reconcile instead of retrying.
  /// </summary>
  public enum OrderDispatchOutcome
  {
    NoResponse = 0,
    Rejected = 1,
    Accepted = 2,
    PartiallyFilled = 3,
    Filled = 4
  }

  public class OrderDispatchResult
  {
    public OrderDispatchOutcome Outcome { get; set; }

    public string BrokerOrderId { get; set; }

    public string ErrorCode { get; set; }

    public List<OrderEventPayload> Events { get; set; } = new List<OrderEventPayload>();
  }

  /// <summary>What a reconciliation query can learn. `Unknown` means the outcome is still not known.</summary>
  public enum OrderQueryOutcome
  {
    Unknown = 0,
    Rejected = 1,
    Accepted = 2,
    PartiallyFilled = 3,
    Filled = 4
  }

  public class OrderQueryResult
  {
    public OrderQueryOutcome Outcome { get; set; }

    public string BrokerOrderId { get; set; }

    public int FilledVolumeUnits { get; set; }

    public double? AveragePrice { get; set; }

    public string ErrorCode { get; set; }

    public List<OrderEventPayload> Events { get; set; } = new List<OrderEventPayload>();
  }

  /// <summary>
  /// The seam between the execution engine and whoever accepts orders. Behind it live the simulated gateway
  /// used while the broker application is not approved and the cTrader gateway that will replace it; above it
  /// nothing knows which one is active.
  /// </summary>
  public interface IExecutionGateway
  {
    Task<OrderDispatchResult> SendAsync(OrderRequest request, CancellationToken cancellationToken);

    /// <summary>Reads the state of an order already sent, for a reconciliation that cannot retry blindly.</summary>
    Task<OrderQueryResult> QueryAsync(string clientOrderId, string symbol, CancellationToken cancellationToken);
  }
}
