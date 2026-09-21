using System;
using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Query.Risk
{
    /// <summary>
    /// Evaluates the current risk decision for a basket. Read-only: it builds an input from the stored
    /// state and returns the gates, without changing anything.
    /// </summary>
    public class GetBasketRiskDecision : IRequest<RiskDecisionDto>
    {
        public Guid BasketId { get; set; }
    }
}
