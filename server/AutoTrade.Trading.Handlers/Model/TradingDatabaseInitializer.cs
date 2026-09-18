using System;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AutoTrade.Trading.Handlers.Model
{
  /// <summary>
  /// Creates the local development schema and the singleton operational rows.
  /// The bootstrap operator password is never stored in configuration files: it must come from
  /// environment variables or a secret store, otherwise no operator is created.
  /// </summary>
  public class TradingDatabaseInitializer(DB db, IPasswordHasher<Operator> passwordHasher, IConfiguration configuration, TimeProvider timeProvider, ILogger<TradingDatabaseInitializer> logger)
  {
    private const int KillSwitchStateId = 1;
    private const int MarketManagerStateId = 1;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
      if (configuration.GetValue<bool>("Database:EnsureCreatedOnStartup"))
      {
        await db.Database.EnsureCreatedAsync(cancellationToken);
      }

      await EnsureKillSwitchAsync(cancellationToken);
      await EnsureMarketManagerStateAsync(cancellationToken);
      await EnsureBootstrapOperatorAsync(cancellationToken);
    }

    private async Task EnsureKillSwitchAsync(CancellationToken cancellationToken)
    {
      KillSwitchState killSwitch = await db.KillSwitchStates.FirstOrDefaultAsync(item => item.Id == KillSwitchStateId, cancellationToken);
      if (killSwitch != null)
      {
        return;
      }

      db.KillSwitchStates.Add(new KillSwitchState
      {
        Id = KillSwitchStateId,
        IsEngaged = false,
        ChangedAtUtc = null,
        ChangedByOperatorId = null,
        Reason = null
      });

      await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureMarketManagerStateAsync(CancellationToken cancellationToken)
    {
      MarketManagerState marketManager = await db.MarketManagerStates.FirstOrDefaultAsync(item => item.Id == MarketManagerStateId, cancellationToken);
      if (marketManager != null)
      {
        return;
      }

      db.MarketManagerStates.Add(new MarketManagerState
      {
        Id = MarketManagerStateId,
        Mode = MarketManagerMode.Supervised,
        IsAnalysisRunning = false,
        UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
      });

      await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureBootstrapOperatorAsync(CancellationToken cancellationToken)
    {
      string userName = configuration["Trading:Bootstrap:UserName"];
      string password = configuration["Trading:Bootstrap:Password"];

      if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
      {
        logger.LogWarning("No bootstrap operator configured. Set Trading:Bootstrap:UserName and Trading:Bootstrap:Password through environment variables or a secret store.");
        return;
      }

      Operator existingOperator = await db.Operators.FirstOrDefaultAsync(item => item.UserName == userName, cancellationToken);
      if (existingOperator != null)
      {
        return;
      }

      Operator newOperator = new Operator
      {
        Id = Guid.CreateVersion7(),
        UserName = userName,
        IsActive = true,
        FailedAttempts = 0,
        LockedUntilUtc = null,
        CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
        LastLoginAtUtc = null
      };

      newOperator.PasswordHash = passwordHasher.HashPassword(newOperator, password);

      db.Operators.Add(newOperator);
      await db.SaveChangesAsync(cancellationToken);

      logger.LogInformation("Bootstrap operator created. UserName={UserName}", userName);
    }
  }
}
