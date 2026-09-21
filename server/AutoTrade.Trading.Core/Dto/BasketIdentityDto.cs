namespace AutoTrade.Trading.Core.Dto
{
    /// <summary>
    /// Request body for identity-scoped operations. The basket key comes from the route and the
    /// acting operator from the authenticated context, so neither travels in the body.
    /// </summary>
    public class BasketIdentityDto
    {
        public string Name { get; set; }
    }
}
