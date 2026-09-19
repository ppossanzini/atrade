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

      // The frozen policy is reported with the same contract as the draft, because it is the same strategy:
      // one in preparation and one in force.
      CreateMap<BasketVersionPolicy, BasketPolicyDto>();
    }
  }
}
