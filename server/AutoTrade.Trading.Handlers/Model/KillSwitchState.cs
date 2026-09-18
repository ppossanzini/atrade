using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoTrade.Trading.Handlers.Model
{
  [Table("KillSwitchState")]
  public class KillSwitchState
  {
    [Key]
    public int Id { get; set; }

    public bool IsEngaged { get; set; }

    public DateTime? ChangedAtUtc { get; set; }

    public Guid? ChangedByOperatorId { get; set; }

    [StringLength(256)]
    public string Reason { get; set; }
  }
}
