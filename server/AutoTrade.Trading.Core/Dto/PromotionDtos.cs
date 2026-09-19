using System.Collections.Generic;

namespace AutoTrade.Trading.Core.Dto
{
  /// <summary>
  /// Whether one requirement of the live promotion gate has been met. The distinction that matters is
  /// between a requirement the system can measure and one it cannot: reporting the second as unmet would
  /// be as wrong as reporting it as met, because in both cases the operator would be reading a claim
  /// nobody made.
  /// </summary>
  public enum PromotionRequirementState
  {
    /// <summary>The system measured it and the requirement holds.</summary>
    Satisfied = 0,

    /// <summary>The system measured it and the requirement does not hold yet.</summary>
    NotSatisfied = 1,

    /// <summary>
    /// The requirement needs evidence the application does not own: an approval, a drill or a review.
    /// It stays open until a person closes it, and the system says so instead of guessing.
    /// </summary>
    NotVerifiable = 2
  }

  /// <summary>One requirement of the promotion gate, with the evidence behind its state.</summary>
  public class PromotionRequirementDto
  {
    /// <summary>Stable key of the requirement, part of the audit contract.</summary>
    public string Key { get; set; }

    public PromotionRequirementState State { get; set; }

    /// <summary>What the system actually observed, or null when it can observe nothing.</summary>
    public string Evidence { get; set; }
  }

  /// <summary>
  /// Read-only state of the live promotion gate. Live is not part of the demo MVP, so this reports why
  /// it stays closed rather than offering a way to open it.
  /// </summary>
  public class PromotionStatusDto
  {
    /// <summary>True only when every requirement is satisfied. There is no partial eligibility.</summary>
    public bool IsLiveEligible { get; set; }

    /// <summary>Environment of the broker account in force, or null when no account is configured.</summary>
    public string CurrentEnvironment { get; set; }

    public List<PromotionRequirementDto> Requirements { get; set; }
  }
}
