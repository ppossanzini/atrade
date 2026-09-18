using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Handlers.Model
{
  /// <summary>
  /// Stored provider grant for one environment. Tokens are persisted only as ciphertext produced by
  /// the token protector: the plaintext is never written to the database and never logged.
  /// </summary>
  [Table("BrokerAuthorization")]
  public class BrokerAuthorization
  {
    [Key]
    public Guid Id { get; set; }

    public TradingEnvironment Environment { get; set; }

    public long? CtidTraderAccountId { get; set; }

    public string AccessTokenCipher { get; set; }

    public string RefreshTokenCipher { get; set; }

    public DateTime AccessTokenExpiresAtUtc { get; set; }

    public DateTime AuthorizedAtUtc { get; set; }

    public Guid AuthorizedByOperatorId { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public string LastError { get; set; }
  }
}
