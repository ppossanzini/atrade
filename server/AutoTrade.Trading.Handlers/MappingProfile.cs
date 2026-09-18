using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Handlers.Model;
using MapZilla;

namespace AutoTrade.Trading.Handlers
{
  public class MappingProfile : Profile
  {
    public MappingProfile()
    {
      CreateMap<KillSwitchState, KillSwitchStatusDto>();
      CreateMap<TradingAccount, TradingAccountStatusDto>();
      CreateMap<MarketManagerState, MarketManagerStatusDto>();

      CreateMap<BasketDraftLeg, BasketCompositionLegDto>();
      CreateMap<BasketDraftPolicy, BasketPolicyDto>();
    }
  }
}
