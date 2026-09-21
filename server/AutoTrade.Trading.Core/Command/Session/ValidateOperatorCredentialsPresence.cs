using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Session
{
    public class ValidateOperatorCredentialsPresence : IRequest<bool>
    {
        public string UserName { get; set; }
        public string Password { get; set; }
    }
}
