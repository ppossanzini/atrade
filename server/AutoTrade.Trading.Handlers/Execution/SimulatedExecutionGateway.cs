using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Handlers.Execution
{
  /// <summary>
  /// Gateway used while the broker application is not approved. It is deterministic and driven by configured
  /// profiles, so every path the engine must survive can be reproduced: a clean fill, a partial fill that
  /// violates a policy, a rejection, silence that has to become a timeout, and a repeated event that must be
  /// deduplicated instead of double counted.
  ///
  /// It implements the same seam as the real gateway: nothing above it knows which one is active.
  /// </summary>
  public class SimulatedExecutionGateway(ExecutionOptions options) : IExecutionGateway
  {
    private const string FilledBehaviour = "Filled";
    private const string PartialFillBehaviour = "PartialFill";
    private const string RejectedBehaviour = "Rejected";
    private const string NoResponseBehaviour = "NoResponse";

    public Task<OrderDispatchResult> SendAsync(OrderRequest request, CancellationToken cancellationToken)
    {
      SimulatedSymbolBehaviour behaviour = Resolve(options, request.Symbol);
      string brokerOrderId = "SIM-" + request.ClientOrderId;

      switch (Normalise(behaviour != null ? behaviour.Behaviour : null) ?? Normalise(options.Simulated != null ? options.Simulated.DefaultBehaviour : null) ?? FilledBehaviour)
      {
        case NoResponseBehaviour:
          // Silence is not a rejection: nothing is claimed about the order, and the caller has to reconcile.
          return Task.FromResult(new OrderDispatchResult { Outcome = OrderDispatchOutcome.NoResponse });

        case RejectedBehaviour:
          return Task.FromResult(new OrderDispatchResult
          {
            Outcome = OrderDispatchOutcome.Rejected,
            BrokerOrderId = brokerOrderId,
            ErrorCode = string.IsNullOrWhiteSpace(behaviour.ErrorCode) ? "SIM_REJECTED" : behaviour.ErrorCode,
            Events = new List<OrderEventPayload>
            {
              Event(request, brokerOrderId, ExecutionEventKind.OrderRejected, 0, null)
            }
          });

        case PartialFillBehaviour:
          int partialVolume = (int)Math.Floor(request.VolumeUnits * Clamp(behaviour.FillRatio));

          return Task.FromResult(new OrderDispatchResult
          {
            Outcome = OrderDispatchOutcome.PartiallyFilled,
            BrokerOrderId = brokerOrderId,
            Events = WithOptionalDuplicate(
              behaviour,
              new List<OrderEventPayload>
              {
                Event(request, brokerOrderId, ExecutionEventKind.OrderAccepted, 0, null),
                Event(request, brokerOrderId, ExecutionEventKind.OrderPartiallyFilled, partialVolume, 1.0)
              })
          });

        default:
          return Task.FromResult(new OrderDispatchResult
          {
            Outcome = OrderDispatchOutcome.Filled,
            BrokerOrderId = brokerOrderId,
            Events = WithOptionalDuplicate(
              behaviour,
              new List<OrderEventPayload>
              {
                Event(request, brokerOrderId, ExecutionEventKind.OrderAccepted, 0, null),
                Event(request, brokerOrderId, ExecutionEventKind.OrderFilled, request.VolumeUnits, 1.0)
              })
          });
      }
    }

    public Task<OrderQueryResult> QueryAsync(string clientOrderId, string symbol, CancellationToken cancellationToken)
    {
      SimulatedSymbolBehaviour behaviour = Resolve(options, symbol);
      string answer = Normalise(behaviour != null ? behaviour.AfterQueryBehaviour : null)
        ?? Normalise(options.Simulated != null ? options.Simulated.DefaultAfterQueryBehaviour : null)
        ?? FilledBehaviour;

      OrderQueryResult result = new OrderQueryResult
      {
        BrokerOrderId = "SIM-" + clientOrderId
      };

      switch (answer)
      {
        case RejectedBehaviour:
          result.Outcome = OrderQueryOutcome.Rejected;
          result.ErrorCode = behaviour != null && !string.IsNullOrWhiteSpace(behaviour.ErrorCode) ? behaviour.ErrorCode : "SIM_REJECTED";
          result.Events.Add(new OrderEventPayload
          {
            BrokerEventId = "SIM-" + clientOrderId + "-rejected",
            Kind = ExecutionEventKind.OrderRejected,
            Symbol = symbol
          });
          break;

        case PartialFillBehaviour:
        case "PartiallyFilled":
          result.Outcome = OrderQueryOutcome.PartiallyFilled;
          result.FilledVolumeUnits = 0;
          break;

        case FilledBehaviour:
          result.Outcome = OrderQueryOutcome.Filled;
          result.Events.Add(new OrderEventPayload
          {
            BrokerEventId = "SIM-" + clientOrderId + "-filled",
            Kind = ExecutionEventKind.OrderFilled,
            Symbol = symbol
          });
          break;

        default:
          // The outcome is still not known: the execution stays in reconciliation instead of pretending.
          result.Outcome = OrderQueryOutcome.Unknown;
          break;
      }

      return Task.FromResult(result);
    }

    private static OrderEventPayload Event(OrderRequest request, string brokerOrderId, ExecutionEventKind kind, int filledVolumeUnits, double? averagePrice)
    {
      return new OrderEventPayload
      {
        // The identity depends on the client order id and the kind, so a repeated send of the same leg
        // produces the same identity and the dedup can recognise it.
        BrokerEventId = "SIM-" + request.ClientOrderId + "-" + kind.ToString().ToLowerInvariant(),
        Kind = kind,
        Symbol = request.Symbol,
        FilledVolumeUnits = filledVolumeUnits,
        AveragePrice = averagePrice,
        Payload = "brokerOrderId=" + brokerOrderId + ";kind=" + kind
      };
    }

    private static List<OrderEventPayload> WithOptionalDuplicate(SimulatedSymbolBehaviour behaviour, List<OrderEventPayload> events)
    {
      if (behaviour != null && behaviour.DuplicateEvent && events.Count > 0)
      {
        events.Add(events[events.Count - 1]);
      }

      return events;
    }

    private static SimulatedSymbolBehaviour Resolve(ExecutionOptions options, string symbol)
    {
      if (options.Simulated == null || options.Simulated.Symbols == null || string.IsNullOrWhiteSpace(symbol))
      {
        return null;
      }

      return options.Simulated.Symbols.TryGetValue(symbol, out SimulatedSymbolBehaviour behaviour) ? behaviour : null;
    }

    private static string Normalise(string value)
    {
      return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static double Clamp(double ratio)
    {
      if (ratio <= 0)
      {
        return 0;
      }

      return ratio >= 1 ? 1 : ratio;
    }
  }
}
