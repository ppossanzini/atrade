using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AutoTrade.Trading.Handlers.Evidence
{
  /// <summary>
  /// Turns facts the system already recorded into the text that gets embedded.
  /// </summary>
  /// <remarks>
  /// The rendering is deliberately plain and complete rather than elegant: it is read by a model that will
  /// compare it with other episodes, so what matters is that every number present is a number that was
  /// measured, and that the same facts always produce the same sentence.
  /// </remarks>
  public static class OperationalEpisodeRenderer
  {
    /// <summary>
    /// Version of the wording. It is part of a collection's identity, so changing any sentence below means
    /// bumping this value: the same facts would otherwise produce vectors that silently disagree with the ones
    /// already stored.
    /// </summary>
    public const string TextVersion = "v1";

    public static OperationalEpisode Create(
      OperationalEpisodeKind kind,
      string sourceRef,
      DateTime occurredAtUtc,
      OperationalEpisodeContext context,
      OperationalEpisodeOutcome outcome)
    {
      return new OperationalEpisode
      {
        EpisodeId = Guid.CreateVersion7(),
        Kind = kind,
        OccurredAtUtc = occurredAtUtc,
        SourceRef = sourceRef,
        Text = Render(kind, context, outcome),
        TextVersion = TextVersion
      };
    }

    public static string Render(OperationalEpisodeKind kind, OperationalEpisodeContext context, OperationalEpisodeOutcome outcome)
    {
      StringBuilder text = new StringBuilder();

      text.Append(Opening(kind));

      if (context != null)
      {
        text.Append(' ');
        text.Append("Basket version ").Append(context.VersionNumber);
        text.Append(", entry mode ").Append(Named(context.EntryMode));
        text.Append(", action ").Append(Named(context.Action));
        text.Append(", confidence ").Append(Number(context.Confidence)).Append('.');
        text.Append(' ').Append(Situation(context));
        text.Append(' ').Append(Legs(context.Legs));
      }

      if (outcome != null)
      {
        string rendered = Outcome(outcome);

        if (rendered.Length > 0)
        {
          text.Append(' ').Append(rendered);
        }
      }

      return text.ToString();
    }

    private static string Opening(OperationalEpisodeKind kind)
    {
      switch (kind)
      {
        case OperationalEpisodeKind.GateBlocked:
          return "The gate blocked a proposal.";
        case OperationalEpisodeKind.ExecutionCompletedNominal:
          return "An execution was filled completely.";
        case OperationalEpisodeKind.ExecutionCompletedPartial:
          return "An execution was closed without full coverage.";
        case OperationalEpisodeKind.ExecutionCompensated:
          return "A compensating execution was created.";
        case OperationalEpisodeKind.ExecutionRefused:
          return "An execution was refused before it started.";
        case OperationalEpisodeKind.KillSwitchChanged:
          return "The kill switch changed state.";
        default:
          throw new InvalidOperationException("Unsupported episode kind: " + kind);
      }
    }

    private static string Situation(OperationalEpisodeContext context)
    {
      StringBuilder situation = new StringBuilder();

      situation.Append("Situation: coverage ").Append(Number(context.CoveragePercent));

      if (context.MinimumCoveragePercent > 0d)
      {
        situation.Append(" of a required ").Append(Number(context.MinimumCoveragePercent));
      }

      situation.Append(" percent, market snapshot age ").Append(Number(context.SnapshotAgeSeconds)).Append(" seconds");

      if (context.DailyLossLimitPercent > 0d)
      {
        situation.Append(", daily loss ").Append(Number(context.DailyLossPercent))
          .Append(" of ").Append(Number(context.DailyLossLimitPercent)).Append(" percent");
      }

      return situation.Append('.').ToString();
    }

    /// <summary>
    /// The legs are listed because a verdict on the basket and a verdict on one leg are different facts, and a
    /// retrieval that cannot see which leg was tight would compare the wrong situations.
    /// </summary>
    private static string Legs(List<OperationalEpisodeLeg> legs)
    {
      if (legs == null || legs.Count == 0)
      {
        return "No leg detail was recorded.";
      }

      StringBuilder text = new StringBuilder("Legs: ");

      for (int index = 0; index < legs.Count; index++)
      {
        OperationalEpisodeLeg leg = legs[index];

        if (index > 0)
        {
          text.Append("; ");
        }

        text.Append(leg.Symbol)
          .Append(" spread ").Append(Number(leg.SpreadPips)).Append(" of ").Append(Number(leg.SpreadLimitPips)).Append(" pips")
          .Append(" and volatility ").Append(Number(leg.VolatilityPercent)).Append(" of ").Append(Number(leg.VolatilityLimitPercent)).Append(" percent");
      }

      return text.Append('.').ToString();
    }

    private static string Outcome(OperationalEpisodeOutcome outcome)
    {
      List<string> parts = new List<string>();

      if (!string.IsNullOrWhiteSpace(outcome.Code))
      {
        StringBuilder rule = new StringBuilder("Outcome: ");

        rule.Append(outcome.Code);

        if (!string.IsNullOrWhiteSpace(outcome.Subject))
        {
          rule.Append(" on ").Append(outcome.Subject);
        }

        if (outcome.ObservedValue.HasValue)
        {
          rule.Append(", observed ").Append(Number(outcome.ObservedValue.Value));

          if (outcome.ThresholdValue.HasValue)
          {
            rule.Append(" against ").Append(Number(outcome.ThresholdValue.Value));
          }

          if (!string.IsNullOrWhiteSpace(outcome.Unit))
          {
            rule.Append(' ').Append(outcome.Unit);
          }
        }

        parts.Add(rule.Append('.').ToString());
      }

      if (!string.IsNullOrWhiteSpace(outcome.Detail))
      {
        // Kept verbatim: the memory quotes the rule, it never paraphrases it.
        parts.Add(outcome.Detail.Trim());
      }

      if (!string.IsNullOrWhiteSpace(outcome.Reason))
      {
        // A reason without a rule code is a complete outcome on its own, which is why it is not prefixed by
        // "Outcome:" when there is no code: the sentence has to read as one statement, not as an empty slot.
        parts.Add("Reason: " + outcome.Reason.Trim() + ".");
      }

      return string.Join(" ", parts);
    }

    private static string Named(string value)
    {
      return string.IsNullOrWhiteSpace(value) ? "unspecified" : value.Trim();
    }

    /// <summary>
    /// Invariant on purpose. Under a comma-decimal culture the same facts would render differently, the vector
    /// would change, and stored episodes would stop being comparable with new ones for a reason nobody could
    /// see from the code.
    /// </summary>
    private static string Number(double value)
    {
      return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
  }
}
