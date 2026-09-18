using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoTrade.Trading.Handlers.Model
{
  /// <summary>
  /// Registry identity. Composition and policy live in the draft tables, and the active version is
  /// a single row, so this entity carries only identity and archival state.
  /// </summary>
  [Table("Basket")]
  public class Basket
  {
    [Key]
    public Guid Id { get; set; }

    [Required]
    [StringLength(128)]
    public string Name { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? ArchivedAtUtc { get; set; }
  }
}
