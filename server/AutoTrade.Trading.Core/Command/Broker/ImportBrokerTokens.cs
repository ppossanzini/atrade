using System;
using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Command.Broker
{
    /// <summary>
    /// Imports a token pair obtained outside the consent flow, which is how the official Playground issues
    /// credentials for the developer's own cTID while an application is still waiting for approval.
    ///
    /// The pair is normalized through the provider refresh endpoint before being stored, so the imported
    /// credential is validated and the stored access token always has a known expiry.
    /// </summary>
    public class ImportBrokerTokens : IRequest<BrokerAuthorizationResultDto>
    {
        public string AccessToken { get; set; }

        public string RefreshToken { get; set; }

        public Guid OperatorId { get; set; }
    }
}
