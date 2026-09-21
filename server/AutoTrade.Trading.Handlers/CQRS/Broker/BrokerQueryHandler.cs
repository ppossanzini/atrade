using System;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Core.Query.Broker;
using AutoTrade.Trading.Handlers.Broker;
using AutoTrade.Trading.Handlers.Model;
using Hikyaku;
using Microsoft.EntityFrameworkCore;

namespace AutoTrade.Trading.Handlers.CQRS.Broker
{
    /// <summary>
    /// Read-only broker status. Tokens are never decrypted here: the projection reports only whether a
    /// grant exists and when it expires.
    /// </summary>
    public class BrokerQueryHandler(DB db, BrokerOptions options, TimeProvider timeProvider) : IRequestHandler<GetBrokerConnectionStatus, BrokerConnectionStatusDto>
    {
        public async Task<BrokerConnectionStatusDto> Handle(GetBrokerConnectionStatus request, CancellationToken cancellationToken)
        {
            BrokerAuthorization authorization = await db.BrokerAuthorizations
              .AsNoTracking()
              .FirstOrDefaultAsync(item => item.Environment == options.Environment, cancellationToken);

            DateTime now = timeProvider.GetUtcNow().UtcDateTime;

            return new BrokerConnectionStatusDto
            {
                IsClientConfigured = options.IsClientConfigured,
                Environment = options.Environment,
                RedirectUri = options.RedirectUri,
                IsAuthorized = authorization != null,
                AuthorizedAtUtc = authorization != null ? authorization.AuthorizedAtUtc : (DateTime?)null,
                AccessTokenExpiresAtUtc = authorization != null ? authorization.AccessTokenExpiresAtUtc : (DateTime?)null,
                IsAccessTokenExpired = authorization != null && authorization.AccessTokenExpiresAtUtc <= now,
                CtidTraderAccountId = authorization != null ? authorization.CtidTraderAccountId : null,
                LastError = authorization != null ? authorization.LastError : null
            };
        }
    }
}
