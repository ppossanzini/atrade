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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      modelBuilder.Entity<Operator>().HasIndex(item => item.UserName).IsUnique();
      modelBuilder.Entity<OperatorSession>().HasIndex(item => item.SessionToken).IsUnique();
      modelBuilder.Entity<JournalEvent>().HasIndex(item => item.Sequence).IsUnique();
      modelBuilder.Entity<JournalEvent>().HasIndex(item => item.OccurredAtUtc);

      base.OnModelCreating(modelBuilder);
    }
  }
}
