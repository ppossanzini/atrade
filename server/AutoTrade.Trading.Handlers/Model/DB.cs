using Microsoft.EntityFrameworkCore;

namespace AutoTrade.Trading.Handlers.Model
{
  public class DB : DbContext
  {
    public DB(DbContextOptions<DB> options) : base(options)
    {
    }

    public DbSet<Operator> Operators { get; set; }

    public DbSet<OperatorSession> OperatorSessions { get; set; }

    public DbSet<TradingAccount> TradingAccounts { get; set; }

    public DbSet<KillSwitchState> KillSwitchStates { get; set; }

    public DbSet<MarketManagerState> MarketManagerStates { get; set; }

    public DbSet<JournalEvent> JournalEvents { get; set; }

    public DbSet<Basket> Baskets { get; set; }

    public DbSet<BasketDraftLeg> BasketDraftLegs { get; set; }

    public DbSet<BasketDraftPolicy> BasketDraftPolicies { get; set; }

    public DbSet<BasketVersion> BasketVersions { get; set; }

    public DbSet<BasketVersionLeg> BasketVersionLegs { get; set; }

    public DbSet<BasketVersionPolicy> BasketVersionPolicies { get; set; }

    public DbSet<ActiveBasketVersion> ActiveBasketVersions { get; set; }

    public DbSet<BrokerAuthorization> BrokerAuthorizations { get; set; }

    public DbSet<BrokerAuthorizationAttempt> BrokerAuthorizationAttempts { get; set; }

    public DbSet<MarketSnapshot> MarketSnapshots { get; set; }

    public DbSet<MarketSnapshotLeg> MarketSnapshotLegs { get; set; }

    public DbSet<Proposal> Proposals { get; set; }

    public DbSet<ProposalLeg> ProposalLegs { get; set; }

    public DbSet<GateEvaluation> GateEvaluations { get; set; }

    public DbSet<ProposalEvidence> ProposalEvidences { get; set; }

    public DbSet<Execution> Executions { get; set; }

    public DbSet<ExecutionLeg> ExecutionLegs { get; set; }

    public DbSet<BrokerEvent> BrokerEvents { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      modelBuilder.Entity<Operator>().HasIndex(item => item.UserName).IsUnique();
      modelBuilder.Entity<OperatorSession>().HasIndex(item => item.SessionToken).IsUnique();
      modelBuilder.Entity<JournalEvent>().HasIndex(item => item.Sequence).IsUnique();
      modelBuilder.Entity<JournalEvent>().HasIndex(item => item.OccurredAtUtc);

      // Name uniqueness is scoped to non-archived baskets, which a database index cannot express,
      // so it stays a handler rule. The index only serves lookups.
      modelBuilder.Entity<Basket>().HasIndex(item => item.Name);
      modelBuilder.Entity<BasketDraftLeg>().HasIndex(item => item.BasketId);
      modelBuilder.Entity<BasketDraftPolicy>().HasIndex(item => item.BasketId);
      modelBuilder.Entity<BasketVersion>().HasIndex(item => new { item.BasketId, item.Number }).IsUnique();
      modelBuilder.Entity<BasketVersionLeg>().HasIndex(item => new { item.VersionId, item.Ordinal }).IsUnique();
      modelBuilder.Entity<BasketVersionLeg>().HasIndex(item => new { item.VersionId, item.Symbol }).IsUnique();
      modelBuilder.Entity<BasketVersionPolicy>().HasIndex(item => item.VersionId).IsUnique();

      // One grant per environment, and one attempt per correlator: both are uniqueness rules the
      // database can enforce, so they belong here instead of in a handler.
      modelBuilder.Entity<BrokerAuthorization>().HasIndex(item => item.Environment).IsUnique();
      modelBuilder.Entity<BrokerAuthorizationAttempt>().HasIndex(item => item.CorrelationHash).IsUnique();
      modelBuilder.Entity<BrokerAuthorizationAttempt>().HasIndex(item => item.ExpiresAtUtc);

      // The operator queue reads open proposals by recency, and every proposal lookup starts from the
      // basket or from the snapshot it refers to.
      modelBuilder.Entity<Proposal>().HasIndex(item => new { item.Status, item.ProposedAtUtc });
      modelBuilder.Entity<Proposal>().HasIndex(item => item.BasketId);
      modelBuilder.Entity<Proposal>().HasIndex(item => item.SnapshotId);
      modelBuilder.Entity<ProposalLeg>().HasIndex(item => new { item.ProposalId, item.Ordinal }).IsUnique();
      modelBuilder.Entity<GateEvaluation>().HasIndex(item => new { item.ProposalId, item.Ordinal }).IsUnique();
      modelBuilder.Entity<ProposalEvidence>().HasIndex(item => new { item.ProposalId, item.Rank }).IsUnique();
      modelBuilder.Entity<MarketSnapshotLeg>().HasIndex(item => new { item.SnapshotId, item.Ordinal }).IsUnique();

      // One proposal produces at most one execution, one leg owns exactly one client order id, and a broker
      // event is applied once: these three invariants are what makes a retry safe, so the database holds them
      // instead of relying on the handler remembering them.
      modelBuilder.Entity<Execution>().HasIndex(item => item.ProposalId).IsUnique();
      modelBuilder.Entity<Execution>().HasIndex(item => new { item.Status, item.CreatedAtUtc });
      modelBuilder.Entity<ExecutionLeg>().HasIndex(item => item.ClientOrderId).IsUnique();
      modelBuilder.Entity<ExecutionLeg>().HasIndex(item => new { item.ExecutionId, item.Ordinal }).IsUnique();
      modelBuilder.Entity<BrokerEvent>().HasIndex(item => item.BrokerEventId).IsUnique();
      modelBuilder.Entity<BrokerEvent>().HasIndex(item => item.ExecutionId);

      base.OnModelCreating(modelBuilder);
    }
  }
}
