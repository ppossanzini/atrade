using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoTrade.Trading.Handlers.Model
{
  [Table("OperatorSession")]
  public class OperatorSession
  {
    [Key]
    public Guid Id { get; set; }

    public Guid OperatorId { get; set; }

    [Required]
    [StringLength(128)]
    public string SessionToken { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? EndedAtUtc { get; set; }

    [StringLength(64)]
    public string EndedReason { get; set; }
  }
}
