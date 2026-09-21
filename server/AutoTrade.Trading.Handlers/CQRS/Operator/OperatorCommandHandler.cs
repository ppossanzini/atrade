using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Command.Session;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.CQRS.Journal;
using AutoTrade.Trading.Handlers.Model;
using Hikyaku;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OperatorEntity = AutoTrade.Trading.Handlers.Model.Operator;

namespace AutoTrade.Trading.Handlers.CQRS.Operator
{
    public class OperatorCommandHandler(DB db, IHikyaku hikyaku, IPasswordHasher<OperatorEntity> passwordHasher, IJournalWriter journalWriter, IConfiguration configuration, TimeProvider timeProvider, ILogger<OperatorCommandHandler> logger)
      : IRequestHandler<LoginOperator, LoginOperatorResult>,
        IRequestHandler<LogoutOperator, Unit>,
        IRequestHandler<ValidateOperatorCredentialsPresence, bool>,
        IRequestHandler<ValidateOperatorCredentialsMatching, OperatorCredentialsValidationResult>
    {
        private const string OperatorEntityType = "Operator";
        private const string SessionEntityType = "OperatorSession";
        private const int SessionTokenBytes = 32;
        private const string LogoutReason = "operator_logout";

        public async Task<LoginOperatorResult> Handle(LoginOperator request, CancellationToken cancellationToken)
        {
            LoginOperatorResult result = new LoginOperatorResult
            {
                Outcome = LoginOutcome.InvalidCredentials
            };

            bool presenceValid = await hikyaku.Send(new ValidateOperatorCredentialsPresence
            {
                UserName = request.UserName,
                Password = request.Password
            }, cancellationToken);

            if (!presenceValid)
            {
                logger.LogWarning("Login rejected because credentials were not provided.");
                await journalWriter.AppendSystemEventAsync(JournalEventKind.OperatorLoginFailed, OperatorEntityType, null, "reason=missing_credentials", cancellationToken);
                return result;
            }

            OperatorCredentialsValidationResult validation = await hikyaku.Send(new ValidateOperatorCredentialsMatching
            {
                UserName = request.UserName,
                Password = request.Password
            }, cancellationToken);

            if (!validation.IsFound)
            {
                logger.LogWarning("Login rejected for an unknown operator.");
                await journalWriter.AppendSystemEventAsync(JournalEventKind.OperatorLoginFailed, OperatorEntityType, null, "reason=unknown_operator", cancellationToken);
                return result;
            }

            if (validation.IsLockedOut)
            {
                logger.LogWarning("Login rejected because the operator is locked out. OperatorId={OperatorId}", validation.OperatorId);
                await journalWriter.AppendOperatorEventAsync(validation.OperatorId, JournalEventKind.OperatorLoginFailed, OperatorEntityType, validation.OperatorId, "reason=locked_out", cancellationToken);
                result.Outcome = LoginOutcome.LockedOut;
                return result;
            }

            if (!validation.IsActive)
            {
                logger.LogWarning("Login rejected because the operator is inactive. OperatorId={OperatorId}", validation.OperatorId);
                await journalWriter.AppendOperatorEventAsync(validation.OperatorId, JournalEventKind.OperatorLoginFailed, OperatorEntityType, validation.OperatorId, "reason=inactive", cancellationToken);
                result.Outcome = LoginOutcome.AccountInactive;
                return result;
            }

            if (!validation.IsValid)
            {
                await RegisterFailedAttemptAsync(validation.OperatorId, cancellationToken);
                return result;
            }

            OperatorSession session = await CreateSessionAsync(validation.OperatorId, cancellationToken);

            logger.LogInformation("Operator logged in. OperatorId={OperatorId} SessionId={SessionId}", validation.OperatorId, session.Id);
            await journalWriter.AppendOperatorEventAsync(validation.OperatorId, JournalEventKind.OperatorLoginSucceeded, SessionEntityType, session.Id, "outcome=success", cancellationToken);

            result.Outcome = LoginOutcome.Success;
            result.OperatorId = validation.OperatorId;
            result.SessionId = session.Id;
            result.SessionToken = session.SessionToken;
            result.ExpiresAtUtc = session.ExpiresAtUtc;

            return result;
        }

        public async Task<Unit> Handle(LogoutOperator request, CancellationToken cancellationToken)
        {
            OperatorSession session = await db.OperatorSessions.FirstOrDefaultAsync(
              item => item.SessionToken == request.SessionToken && item.OperatorId == request.OperatorId && item.EndedAtUtc == null,
              cancellationToken);

            if (session == null)
            {
                return Unit.Value;
            }

            session.EndedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            session.EndedReason = LogoutReason;

            await db.SaveChangesAsync(cancellationToken);
            await journalWriter.AppendOperatorEventAsync(request.OperatorId, JournalEventKind.OperatorLogout, SessionEntityType, session.Id, "reason=operator_logout", cancellationToken);

            return Unit.Value;
        }

        public Task<bool> Handle(ValidateOperatorCredentialsPresence request, CancellationToken cancellationToken)
        {
            bool hasUserName = !string.IsNullOrWhiteSpace(request.UserName);
            bool hasPassword = !string.IsNullOrWhiteSpace(request.Password);
            bool userNameFits = hasUserName && request.UserName.Trim().Length <= 128;
            bool passwordFits = hasPassword && request.Password.Length <= 256;

            return Task.FromResult(hasUserName && hasPassword && userNameFits && passwordFits);
        }

        public async Task<OperatorCredentialsValidationResult> Handle(ValidateOperatorCredentialsMatching request, CancellationToken cancellationToken)
        {
            OperatorCredentialsValidationResult result = new OperatorCredentialsValidationResult();
            string userName = request.UserName.Trim();

            OperatorEntity operatorEntity = await db.Operators.AsNoTracking().FirstOrDefaultAsync(item => item.UserName == userName, cancellationToken);

            if (operatorEntity == null)
            {
                return result;
            }

            result.IsFound = true;
            result.OperatorId = operatorEntity.Id;
            result.IsActive = operatorEntity.IsActive;

            if (operatorEntity.LockedUntilUtc.HasValue && operatorEntity.LockedUntilUtc.Value > timeProvider.GetUtcNow().UtcDateTime)
            {
                result.IsLockedOut = true;
                return result;
            }

            if (!operatorEntity.IsActive)
            {
                return result;
            }

            PasswordVerificationResult verification = passwordHasher.VerifyHashedPassword(operatorEntity, operatorEntity.PasswordHash, request.Password);
            result.IsValid = verification != PasswordVerificationResult.Failed;

            return result;
        }

        private async Task RegisterFailedAttemptAsync(Guid operatorId, CancellationToken cancellationToken)
        {
            OperatorEntity operatorEntity = await db.Operators.FirstOrDefaultAsync(item => item.Id == operatorId, cancellationToken);
            if (operatorEntity == null)
            {
                return;
            }

            operatorEntity.FailedAttempts = operatorEntity.FailedAttempts + 1;

            int maxFailedAttempts = configuration.GetValue<int>("Trading:Session:MaxFailedAttempts");
            if (operatorEntity.FailedAttempts >= maxFailedAttempts)
            {
                int lockoutMinutes = configuration.GetValue<int>("Trading:Session:LockoutMinutes");
                operatorEntity.LockedUntilUtc = timeProvider.GetUtcNow().UtcDateTime.AddMinutes(lockoutMinutes);
                logger.LogWarning("Operator locked out after repeated failures. OperatorId={OperatorId}", operatorId);
            }

            await db.SaveChangesAsync(cancellationToken);
            await journalWriter.AppendOperatorEventAsync(operatorId, JournalEventKind.OperatorLoginFailed, OperatorEntityType, operatorId, "reason=invalid_credentials", cancellationToken);
        }

        private async Task<OperatorSession> CreateSessionAsync(Guid operatorId, CancellationToken cancellationToken)
        {
            DateTime now = timeProvider.GetUtcNow().UtcDateTime;
            int sessionMinutes = configuration.GetValue<int>("Trading:Session:SessionMinutes");

            OperatorSession session = new OperatorSession
            {
                Id = Guid.CreateVersion7(),
                OperatorId = operatorId,
                SessionToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(SessionTokenBytes)),
                StartedAtUtc = now,
                ExpiresAtUtc = now.AddMinutes(sessionMinutes),
                EndedAtUtc = null,
                EndedReason = null
            };

            db.OperatorSessions.Add(session);

            OperatorEntity operatorEntity = await db.Operators.FirstOrDefaultAsync(item => item.Id == operatorId, cancellationToken);
            if (operatorEntity != null)
            {
                operatorEntity.FailedAttempts = 0;
                operatorEntity.LockedUntilUtc = null;
                operatorEntity.LastLoginAtUtc = now;
            }

            await db.SaveChangesAsync(cancellationToken);

            return session;
        }
    }
}
