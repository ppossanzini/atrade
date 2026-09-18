using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoTrade.Trading.Handlers.Model
{
  [Table("Operator")]
  public class Operator
  {
    [Key]
    public Guid Id { get; set; }

    [Required]
    [StringLength(128)]
    public string UserName { get; set; }

    [Required]
    [StringLength(512)]
    public string PasswordHash { get; set; }

    public bool IsActive { get; set; }

    public int FailedAttempts { get; set; }

    public DateTime? LockedUntilUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? LastLoginAtUtc { get; set; }
  }
}
