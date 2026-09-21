using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.Evidence;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.Evidence
{
  /// <summary>
  /// Covers building an episode from a blocked proposal and writing it to memory. The gate data used here is
  /// the shape the engine actually produces, including the non-blocking gates, because the situation an episode
  /// remembers is reconstructed from those evaluations.
  /// </summary>
  public class OperationalEpisodeTests
  {
    private static EpisodeProposalFacts Facts()
    {
      return new EpisodeProposalFacts
      {
        ProposalId = Guid.CreateVersion7(),
        VersionNumber = 3,
        EntryMode = "RegimeMomentum",
        Action = "Entry",
        Confidence = 100d
      };
    }

    /// <summary>The ten gates of a real blocked cycle, in evaluation order.</summary>
    private static List<RiskGateResultDto> RealGates()
    {
      return new List<RiskGateResultDto>
      {
        Allow(RiskGateCode.ActiveVersionMissing, "basket", 3d, null, null, "An active version is in force."),
        Allow(RiskGateCode.KillSwitchEngaged, "basket", null, null, null, "The kill switch is released."),
        Allow(RiskGateCode.SnapshotStale, "basket", 0.002d, 60d, "seconds", "The market snapshot is within the validity window."),
        Allow(RiskGateCode.CoverageBelowMinimum, "basket", 100d, 100d, "percent", "Every selected leg is executable."),
        new RiskGateResultDto
        {
          Code = RiskGateCode.RiskPerBasketExceeded,
          Verdict = RiskGateVerdict.Block,
          Subject = "basket",
          ObservedValue = 2.3d,
          ThresholdValue = 0.8d,
          Unit = "percent",
          Detail = "The basket risk exceeds the risk per basket limit."
        },
        Allow(RiskGateCode.DailyLossExceeded, "basket", 0d, 2.5d, "percent", "The daily loss is within the limit."),
        Allow(RiskGateCode.LegSpreadExceeded, "EURUSD", 0.802d, 1.5d, "pips", "The spread is within the limit configured for market Fx."),
        Allow(RiskGateCode.LegVolatilityExceeded, "EURUSD", 0.22d, 0.35d, "percent", "The volatility is within the limit configured for market Fx."),
        Allow(RiskGateCode.LegSpreadExceeded, "XAUUSD", 29.202d, 40d, "pips", "The spread is within the limit configured for market Metal."),
        Allow(RiskGateCode.LegVolatilityExceeded, "XAUUSD", 0.574d, 0.8d, "percent", "The volatility is within the limit configured for market Metal.")
      };
    }

    private static RiskGateResultDto Allow(RiskGateCode code, string subject, double? observed, double? threshold, string unit, string detail)
    {
      return new RiskGateResultDto
      {
        Code = code,
        Verdict = RiskGateVerdict.Allow,
        Subject = subject,
        ObservedValue = observed,
        ThresholdValue = threshold,
        Unit = unit,
        Detail = detail
      };
    }

    [Fact]
    public void GateBlocked_RebuildsTheSituationFromTheGatesThemselves()
    {
      OperationalEpisode episode = OperationalEpisodeBuilder.GateBlocked(Facts(), RealGates(), new DateTime(2026, 9, 21, 9, 0, 0, DateTimeKind.Utc));

      Assert.Equal(OperationalEpisodeKind.GateBlocked, episode.Kind);
      Assert.StartsWith("proposal:", episode.SourceRef, StringComparison.Ordinal);

      // The numbers come from the gates, so an episode can never quote a situation that disagrees with the
      // verdict it is describing.
      Assert.Contains("coverage 100 of a required 100 percent", episode.Text, StringComparison.Ordinal);
      Assert.Contains("snapshot age 0.002 seconds", episode.Text, StringComparison.Ordinal);
      Assert.Contains("daily loss 0 of 2.5 percent", episode.Text, StringComparison.Ordinal);
      Assert.Contains("EURUSD spread 0.802 of 1.5 pips", episode.Text, StringComparison.Ordinal);
      Assert.Contains("XAUUSD spread 29.202 of 40 pips", episode.Text, StringComparison.Ordinal);
      Assert.Contains("Outcome: RiskPerBasketExceeded on basket, observed 2.3 against 0.8 percent", episode.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GateBlocked_IsDeterministic()
    {
      DateTime occurredAt = new DateTime(2026, 9, 21, 9, 0, 0, DateTimeKind.Utc);
      EpisodeProposalFacts facts = Facts();

      string first = OperationalEpisodeBuilder.GateBlocked(facts, RealGates(), occurredAt).Text;
      string second = OperationalEpisodeBuilder.GateBlocked(facts, RealGates(), occurredAt).Text;

      // Same facts, same text: otherwise the same situation would produce two different vectors.
      Assert.Equal(first, second);
    }

    [Fact]
    public void GateBlocked_WhenSeveralRulesBlock_ReportsTheFirstAndDoesNotInventACause()
    {
      List<RiskGateResultDto> gates = new List<RiskGateResultDto>
      {
        Allow(RiskGateCode.CoverageBelowMinimum, "basket", 50d, 100d, "percent", "Some legs are not executable."),
        new RiskGateResultDto { Code = RiskGateCode.RiskPerBasketExceeded, Verdict = RiskGateVerdict.Block, Subject = "basket", ObservedValue = 4d, ThresholdValue = 0.8d, Unit = "percent", Detail = "The basket risk exceeds the risk per basket limit." },
        new RiskGateResultDto { Code = RiskGateCode.DailyLossExceeded, Verdict = RiskGateVerdict.Block, Subject = "basket", ObservedValue = 9d, ThresholdValue = 2.5d, Unit = "percent", Detail = "The daily loss exceeds the limit." }
      };

      OperationalEpisode episode = OperationalEpisodeBuilder.GateBlocked(Facts(), gates, DateTime.UtcNow);

      Assert.Contains("Outcome: RiskPerBasketExceeded", episode.Text, StringComparison.Ordinal);
      Assert.DoesNotContain("DailyLossExceeded on basket, observed", episode.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GateBlocked_WithoutABlockingGate_RefusesInsteadOfRememberingNothing()
    {
      List<RiskGateResultDto> gates = new List<RiskGateResultDto>
      {
        Allow(RiskGateCode.CoverageBelowMinimum, "basket", 100d, 100d, "percent", "Every selected leg is executable.")
      };

      // A "blocked" episode with no blocker would be a memory entry with no cause, which is worse than none.
      Assert.Throws<InvalidOperationException>(() => OperationalEpisodeBuilder.GateBlocked(Facts(), gates, DateTime.UtcNow));
    }

    private static JigenOperationalEpisodeWriter CreateWriter(
      Mock<IJigenEvidenceStore> store,
      Mock<ITextEmbeddingSource> embedding)
    {
      EmbeddingOptions options = new EmbeddingOptions
      {
        Provider = EmbeddingProviderKind.JigenOnnx,
        ModelPath = "/models/model.onnx",
        TokenizerPath = "/models/tokenizer.onnx",
        ModelName = "nomic-embed-text-v1.5",
        TextVersion = "v1",
        Profile = "Nomic"
      };

      return new JigenOperationalEpisodeWriter(store.Object, embedding.Object, options, NullLogger<JigenOperationalEpisodeWriter>.Instance);
    }

    [Fact]
    public async Task Record_StoresTheEpisodeInACollectionThatNamesItsWholeProducer()
    {
      List<EvidenceRecord> stored = new List<EvidenceRecord>();

      Mock<IJigenEvidenceStore> store = new Mock<IJigenEvidenceStore>();
      store.SetupGet(item => item.IsAvailable).Returns(true);
      store
        .Setup(item => item.UpsertAsync(It.IsAny<IReadOnlyList<EvidenceRecord>>(), It.IsAny<CancellationToken>()))
        .Callback<IReadOnlyList<EvidenceRecord>, CancellationToken>((records, token) => stored.AddRange(records))
        .Returns(Task.CompletedTask);

      Mock<ITextEmbeddingSource> embedding = new Mock<ITextEmbeddingSource>();
      embedding.SetupGet(item => item.IsAvailable).Returns(true);
      embedding
        .Setup(item => item.EmbedDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new[] { 0.5f, 0.25f });

      OperationalEpisode episode = OperationalEpisodeBuilder.GateBlocked(Facts(), RealGates(), DateTime.UtcNow);

      await CreateWriter(store, embedding).RecordAsync(episode, CancellationToken.None);

      EvidenceRecord record = Assert.Single(stored);

      Assert.Equal(episode.EpisodeId, record.EvidenceId);
      Assert.Equal(episode.Text, record.Content);
      Assert.Equal(episode.SourceRef, record.SourceRef);
      Assert.Equal(episode.OccurredAtUtc, record.RecordedAtUtc);

      // The collection carries engine, model and text version: a change in any of them has to open a new space
      // rather than mix vectors that were produced differently.
      Assert.Equal("operational-episodes@JigenOnnx@nomic-embed-text-v1.5@v1", record.Collection.EffectiveName);
    }

    [Fact]
    public async Task Record_WhenTheMemoryIsUnavailable_ReportsAndContinues()
    {
      Mock<IJigenEvidenceStore> store = new Mock<IJigenEvidenceStore>();
      store.SetupGet(item => item.IsAvailable).Returns(false);

      Mock<ITextEmbeddingSource> embedding = new Mock<ITextEmbeddingSource>();
      embedding.SetupGet(item => item.IsAvailable).Returns(true);

      OperationalEpisode episode = OperationalEpisodeBuilder.GateBlocked(Facts(), RealGates(), DateTime.UtcNow);

      // The operation this episode describes has already committed, so a missing memory degrades the record and
      // must not become a failure of the caller.
      await CreateWriter(store, embedding).RecordAsync(episode, CancellationToken.None);

      store.Verify(item => item.UpsertAsync(It.IsAny<IReadOnlyList<EvidenceRecord>>(), It.IsAny<CancellationToken>()), Times.Never);
      embedding.Verify(item => item.EmbedDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Record_WhenTheEmbeddingFails_ReportsAndContinues()
    {
      Mock<IJigenEvidenceStore> store = new Mock<IJigenEvidenceStore>();
      store.SetupGet(item => item.IsAvailable).Returns(true);

      Mock<ITextEmbeddingSource> embedding = new Mock<ITextEmbeddingSource>();
      embedding.SetupGet(item => item.IsAvailable).Returns(true);
      embedding
        .Setup(item => item.EmbedDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new InvalidOperationException("the checkpoint could not be read"));

      OperationalEpisode episode = OperationalEpisodeBuilder.GateBlocked(Facts(), RealGates(), DateTime.UtcNow);

      await CreateWriter(store, embedding).RecordAsync(episode, CancellationToken.None);

      store.Verify(item => item.UpsertAsync(It.IsAny<IReadOnlyList<EvidenceRecord>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Record_WhenTheHostIsShuttingDown_DoesNotSwallowTheCancellation()
    {
      Mock<IJigenEvidenceStore> store = new Mock<IJigenEvidenceStore>();
      store.SetupGet(item => item.IsAvailable).Returns(true);

      Mock<ITextEmbeddingSource> embedding = new Mock<ITextEmbeddingSource>();
      embedding.SetupGet(item => item.IsAvailable).Returns(true);

      using CancellationTokenSource source = new CancellationTokenSource();
      await source.CancelAsync();

      embedding
        .Setup(item => item.EmbedDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new OperationCanceledException());

      OperationalEpisode episode = OperationalEpisodeBuilder.GateBlocked(Facts(), RealGates(), DateTime.UtcNow);

      // Best effort covers failures, not shutdown: hiding a cancellation would make the host unkillable.
      await Assert.ThrowsAsync<OperationCanceledException>(() => CreateWriter(store, embedding).RecordAsync(episode, source.Token));
    }
  }
}
