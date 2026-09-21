using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Query.Operations
{
    /// <summary>
    /// Reads the state of the live promotion gate. Read-only on purpose: the gate is closed by an explicit
    /// human decision, never by a command the application can issue.
    /// </summary>
    public class GetPromotionStatus : IRequest<PromotionStatusDto>
    {
    }
}
