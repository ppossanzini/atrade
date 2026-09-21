using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Command.Broker;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Handlers.Broker;
using AutoTrade.Trading.Handlers.Broker.Protocol;
using AutoTrade.Trading.Handlers.Model;
using Hikyaku;
using Microsoft.EntityFrameworkCore;

namespace AutoTrade.Trading.Handlers.CQRS.Broker
{
    /// <summary>
    /// Connectivity probe. It exists because "the broker is not answering" must be diagnosable without
    /// reading logs, and it is gated so it is absent from a deployment that does not need it.
    /// </summary>
    public class BrokerDiagnosticsHandler(DB db, BrokerOptions options, IBrokerTokenProtector tokenProtector, ICtraderProtocolClientFactory clientFactory)
      : IRequestHandler<ProbeBrokerConnection, BrokerProbeResultDto>
    {
        public async Task<BrokerProbeResultDto> Handle(ProbeBrokerConnection request, CancellationToken cancellationToken)
        {
            BrokerProbeResultDto result = new BrokerProbeResultDto();

            if (!options.IsClientConfigured || !options.AllowDiagnostics)
            {
                return result;
            }

            await using (ICtraderProtocolClient client = clientFactory.Create())
            {
                CtraderHandshakeResult handshake = await client.AuthenticateApplicationAsync(cancellationToken);

                result.IsAuthenticated = handshake.IsAuthenticated;
                result.ErrorCode = handshake.ErrorCode;
                result.Description = handshake.Description;

                if (!handshake.IsAuthenticated)
                {
                    return result;
                }

                BrokerAuthorization authorization = await db.BrokerAuthorizations
                  .AsNoTracking()
                  .FirstOrDefaultAsync(item => item.Environment == options.Environment, cancellationToken);

                if (authorization == null)
                {
                    result.Description = "Application authenticated. No stored grant to request accounts for.";

                    return result;
                }

                result.HasStoredGrant = true;

                string accessToken;

                if (!tokenProtector.TryUnprotect(authorization.AccessTokenCipher, out accessToken))
                {
                    result.Description = "Application authenticated. The stored access token could not be decrypted.";

                    return result;
                }

                CtraderAccountListResult accounts = await client.GetAccountsAsync(accessToken, cancellationToken);

                result.HasAccountList = accounts.IsSuccess;
                result.AccountCount = accounts.CtidTraderAccountIds.Count;
                result.CtidTraderAccountIds = new List<long>(accounts.CtidTraderAccountIds);

                if (!accounts.IsSuccess)
                {
                    result.ErrorCode = accounts.ErrorCode;
                    result.Description = accounts.Description;

                    return result;
                }

                result.Description = "Application authenticated and accounts retrieved.";
            }

            return result;
        }
    }
}
