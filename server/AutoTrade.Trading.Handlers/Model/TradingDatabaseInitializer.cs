using System;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.MarketData;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AutoTrade.Trading.Handlers.Model
{
    /// <summary>
    /// Applies the schema migrations and creates the singleton operational rows.
    /// The bootstrap operator password is never stored in configuration files: it must come from
    /// environment variables or a secret store, otherwise no operator is created.
    /// </summary>
    public class TradingDatabaseInitializer(DB db, IPasswordHasher<Operator> passwordHasher, IConfiguration configuration, TimeProvider timeProvider, ILogger<TradingDatabaseInitializer> logger, MarketDataOptions marketDataOptions)
    {
        private const int KillSwitchStateId = 1;
        private const int MarketManagerStateId = 1;

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            // The schema is owned by migrations; nothing is created implicitly from the model.
            await db.Database.MigrateAsync(cancellationToken);

            await EnsureKillSwitchAsync(cancellationToken);
            await EnsureMarketManagerStateAsync(cancellationToken);
            await EnsureBootstrapOperatorAsync(cancellationToken);
            await EnsureMarketDataSourceIsAdmissibleAsync(cancellationToken);
        }

        /// <summary>
        /// A source that is not the broker must never be able to feed a live account. The check runs at
        /// startup, before any request can be served, so a misconfigured deployment fails instead of
        /// reporting an account state nobody observed.
        /// </summary>
        private async Task EnsureMarketDataSourceIsAdmissibleAsync(CancellationToken cancellationToken)
        {
            if (marketDataOptions.Provider == MarketDataProviderKind.None)
            {
                return;
            }

            bool hasLiveAccount = await db.TradingAccounts.AnyAsync(item => item.Environment == TradingEnvironment.Live, cancellationToken);

            if (!hasLiveAccount)
            {
                return;
            }

            logger.LogCritical("Market data provider {Provider} cannot be used with a live account. Remove the live account or configure the broker source.", marketDataOptions.Provider);

            throw new InvalidOperationException("A market data source other than the broker cannot be used while a live account exists.");
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
