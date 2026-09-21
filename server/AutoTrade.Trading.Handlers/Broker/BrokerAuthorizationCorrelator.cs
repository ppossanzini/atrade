using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.Model;
using Microsoft.EntityFrameworkCore;

namespace AutoTrade.Trading.Handlers.Broker
{
    /// <summary>
    /// Result of issuing a correlator. The plaintext value is returned once and never stored.
    /// </summary>
    public class IssuedBrokerCorrelator
    {
        public string CorrelationId { get; set; }

        public DateTime ExpiresAtUtc { get; set; }
    }

    /// <summary>
    /// Manages the single-use OAuth correlator.
    ///
    /// The provider does not echo a state parameter, so the correlator is not carried in the authorization
    /// URL: it is bound to the operator that started the flow and consumed by the callback. Only the hash is
    /// stored, so a database leak does not hand out a usable correlator.
    /// </summary>
    public interface IBrokerAuthorizationCorrelator
    {
        Task<IssuedBrokerCorrelator> IssueAsync(Guid operatorId, TradingEnvironment environment, CancellationToken cancellationToken);

        Task<BrokerAuthorizationAttempt> ConsumeAsync(string correlationId, TradingEnvironment environment, CancellationToken cancellationToken);
    }

    public class BrokerAuthorizationCorrelator(DB db, BrokerOptions options, TimeProvider timeProvider) : IBrokerAuthorizationCorrelator
    {
        /// <summary>32 random bytes, base64url encoded: unguessable without padding or reserved characters.</summary>
        private const int CorrelationSizeBytes = 32;

        public async Task<IssuedBrokerCorrelator> IssueAsync(Guid operatorId, TradingEnvironment environment, CancellationToken cancellationToken)
        {
            DateTime now = timeProvider.GetUtcNow().UtcDateTime;

            // Only one attempt may be pending per environment: starting a new flow invalidates the previous one
            // instead of leaving two valid correlations that the callback would have to disambiguate.
            var pending = await db.BrokerAuthorizationAttempts
              .Where(item => item.Environment == environment && item.ConsumedAtUtc == null && item.ExpiresAtUtc > now)
              .ToListAsync(cancellationToken);

            foreach (BrokerAuthorizationAttempt previous in pending)
            {
                previous.ConsumedAtUtc = now;
            }

            byte[] correlationBytes = RandomNumberGenerator.GetBytes(CorrelationSizeBytes);
            string correlationId = Base64UrlEncode(correlationBytes);
            DateTime expiresAtUtc = now.AddMinutes(options.CorrelationLifetimeMinutes);

            db.BrokerAuthorizationAttempts.Add(new BrokerAuthorizationAttempt
            {
                Id = Guid.CreateVersion7(),
                Environment = environment,
                CorrelationHash = Hash(correlationId),
                CreatedAtUtc = now,
                ExpiresAtUtc = expiresAtUtc,
                ConsumedAtUtc = null,
                CreatedByOperatorId = operatorId
            });

            await db.SaveChangesAsync(cancellationToken);

            return new IssuedBrokerCorrelator
            {
                CorrelationId = correlationId,
                ExpiresAtUtc = expiresAtUtc
            };
        }

        /// <summary>
        /// Validates and consumes the pending attempt. Returns null when no attempt is pending, when the
        /// supplied correlation does not match, when it expired or when it was already consumed.
        /// </summary>
        public async Task<BrokerAuthorizationAttempt> ConsumeAsync(string correlationId, TradingEnvironment environment, CancellationToken cancellationToken)
        {
            DateTime now = timeProvider.GetUtcNow().UtcDateTime;

            IQueryable<BrokerAuthorizationAttempt> query = db.BrokerAuthorizationAttempts
              .Where(item => item.Environment == environment && item.ConsumedAtUtc == null && item.ExpiresAtUtc > now);

            string expectedHash = string.IsNullOrWhiteSpace(correlationId) ? null : Hash(correlationId);

            BrokerAuthorizationAttempt attempt = expectedHash == null
              ? await query.OrderByDescending(item => item.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken)
              : await query.FirstOrDefaultAsync(item => item.CorrelationHash == expectedHash, cancellationToken);

            if (attempt == null)
            {
                return null;
            }

            attempt.ConsumedAtUtc = now;
            await db.SaveChangesAsync(cancellationToken);

            return attempt;
        }

        private static string Hash(string value)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));

            return Convert.ToBase64String(hash);
        }

        private static string Base64UrlEncode(byte[] value)
        {
            return Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
