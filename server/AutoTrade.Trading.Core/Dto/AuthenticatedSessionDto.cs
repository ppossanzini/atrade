namespace AutoTrade.Trading.Core.Dto
{
    public class AuthenticatedSessionDto
    {
        public string AccessToken { get; set; }
        public SessionDto Session { get; set; }
    }
}
