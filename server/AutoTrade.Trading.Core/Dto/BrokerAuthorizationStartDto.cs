using System;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Core.Dto
{
    /// <summary>
    /// Payload for starting the OAuth consent. The authorization URL is built server side so the
    /// client never handles the client secret or duplicates the provider contract.
    /// </summary>
    public class BrokerAuthorizationStartDto
    {
        public BrokerAuthorizationOutcome Outcome { get; set; }
        public string AuthorizationUrl { get; set; }
        public string CorrelationId { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
    }
}
