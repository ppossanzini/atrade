using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Query.Session
{
    public class GetCurrentSessionByToken : IRequest<SessionDto>
    {
        public string SessionToken { get; set; }
    }
}
