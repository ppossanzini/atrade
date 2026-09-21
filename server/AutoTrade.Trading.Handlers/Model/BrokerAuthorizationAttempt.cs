using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Handlers.Model
{
    /// <summary>
    /// Single-use OAuth correlator. Only the hash is stored, so a database leak does not hand out a
    /// usable correlator, and consumption is stamped so a replayed callback is rejected.
    /// </summary>
    [Table("BrokerAuthorizationAttempt")]
    public class BrokerAuthorizationAttempt
    {
        [Key]
        public Guid Id { get; set; }

        public TradingEnvironment Environment { get; set; }

        public string CorrelationHash { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime ExpiresAtUtc { get; set; }

        public DateTime? ConsumedAtUtc { get; set; }

        public Guid CreatedByOperatorId { get; set; }
    }
}
