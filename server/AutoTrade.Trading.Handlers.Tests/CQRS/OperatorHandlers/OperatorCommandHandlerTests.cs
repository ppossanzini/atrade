using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Command.Session;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Enums;
using AutoTrade.Trading.Handlers.Model;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.CQRS.OperatorHandlers
{
  public class OperatorCommandHandlerTests
  {
    private const string UserName = "operator";
    private const string Password = "Dev-Passw0rd-23";

    private static TradingTestContext CreateContext()
    {
      return new TradingTestContext(new Dictionary<string, string>
      {
        { "Trading:Session:SessionMinutes", "240" },
        { "Trading:Session:MaxFailedAttempts", "3" },
        { "Trading:Session:LockoutMinutes", "15" }
      });
    }

    [Fact]
    public async Task LoginOperator_WithValidCredentials_CreatesSessionAndClearsFailures()
    {
      using TradingTestContext context = CreateContext();
      Operator operatorEntity = context.CreateOperator(UserName, Password, true);
      operatorEntity.FailedAttempts = 2;
      context.Db.SaveChanges();

      LoginOperatorResult result = await context.Hikyaku.Send(
        new LoginOperator { UserName = UserName, Password = Password },
        CancellationToken.None);

      Assert.Equal(LoginOutcome.Success, result.Outcome);
      Assert.Equal(operatorEntity.Id, result.OperatorId);
      Assert.NotEqual(Guid.Empty, result.SessionId);
      Assert.False(string.IsNullOrWhiteSpace(result.SessionToken));
      Assert.Equal(context.Clock.GetUtcNow().UtcDateTime.AddMinutes(240), result.ExpiresAtUtc);

      OperatorSession session = await context.Db.OperatorSessions.FirstAsync(CancellationToken.None);
      Assert.Equal(operatorEntity.Id, session.OperatorId);
      Assert.Null(session.EndedAtUtc);

      Operator reloaded = await context.Db.Operators.FirstAsync(CancellationToken.None);
      Assert.Equal(0, reloaded.FailedAttempts);
      Assert.Null(reloaded.LockedUntilUtc);
      Assert.NotNull(reloaded.LastLoginAtUtc);

      Assert.Contains(context.Db.JournalEvents, item => item.Kind == JournalEventKind.OperatorLoginSucceeded);
    }

    [Fact]
    public async Task LoginOperator_WithUnknownOperator_ReturnsInvalidCredentialsWithoutCreatingSession()
    {
      using TradingTestContext context = CreateContext();

      LoginOperatorResult result = await context.Hikyaku.Send(
        new LoginOperator { UserName = "missing", Password = Password },
        CancellationToken.None);

      Assert.Equal(LoginOutcome.InvalidCredentials, result.Outcome);
      Assert.Equal(0, context.Db.OperatorSessions.Count());
      Assert.Contains(context.Db.JournalEvents, item => item.Kind == JournalEventKind.OperatorLoginFailed);
    }

    [Fact]
    public async Task LoginOperator_WithMissingCredentials_ReturnsInvalidCredentials()
    {
      using TradingTestContext context = CreateContext();
      context.CreateOperator(UserName, Password, true);

      LoginOperatorResult result = await context.Hikyaku.Send(
        new LoginOperator { UserName = "   ", Password = string.Empty },
        CancellationToken.None);

      Assert.Equal(LoginOutcome.InvalidCredentials, result.Outcome);
      Assert.Equal(0, context.Db.OperatorSessions.Count());
    }

    [Fact]
    public async Task LoginOperator_WithWrongPassword_IncrementsFailedAttempts()
    {
      using TradingTestContext context = CreateContext();
      Operator operatorEntity = context.CreateOperator(UserName, Password, true);

      LoginOperatorResult result = await context.Hikyaku.Send(
        new LoginOperator { UserName = UserName, Password = "wrong-password" },
        CancellationToken.None);

      Assert.Equal(LoginOutcome.InvalidCredentials, result.Outcome);

      Operator reloaded = await context.Db.Operators.FirstAsync(CancellationToken.None);
      Assert.Equal(1, reloaded.FailedAttempts);
      Assert.Null(reloaded.LockedUntilUtc);
      Assert.Equal(0, context.Db.OperatorSessions.Count());
      Assert.Equal(operatorEntity.Id, reloaded.Id);
    }

    [Fact]
    public async Task LoginOperator_WhenFailuresReachMaximum_LocksOutOperatorAndReportsLockedOut()
    {
      using TradingTestContext context = CreateContext();
      context.CreateOperator(UserName, Password, true);

      for (int attempt = 0; attempt < 3; attempt++)
      {
        await context.Hikyaku.Send(
          new LoginOperator { UserName = UserName, Password = "wrong-password" },
          CancellationToken.None);
      }

      Operator locked = await context.Db.Operators.FirstAsync(CancellationToken.None);
      Assert.Equal(3, locked.FailedAttempts);
      Assert.Equal(context.Clock.GetUtcNow().UtcDateTime.AddMinutes(15), locked.LockedUntilUtc);

      LoginOperatorResult result = await context.Hikyaku.Send(
        new LoginOperator { UserName = UserName, Password = Password },
        CancellationToken.None);

      Assert.Equal(LoginOutcome.LockedOut, result.Outcome);
      Assert.Equal(0, context.Db.OperatorSessions.Count());
    }

    [Fact]
    public async Task LoginOperator_WhenLockoutExpires_AllowsLoginAgain()
    {
      using TradingTestContext context = CreateContext();
      context.CreateOperator(UserName, Password, true);

      for (int attempt = 0; attempt < 3; attempt++)
      {
        await context.Hikyaku.Send(
          new LoginOperator { UserName = UserName, Password = "wrong-password" },
          CancellationToken.None);
      }

      context.Clock.Advance(TimeSpan.FromMinutes(16));

      LoginOperatorResult result = await context.Hikyaku.Send(
        new LoginOperator { UserName = UserName, Password = Password },
        CancellationToken.None);

      Assert.Equal(LoginOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task LoginOperator_WhenOperatorIsInactive_ReturnsAccountInactive()
    {
      using TradingTestContext context = CreateContext();
      context.CreateOperator(UserName, Password, false);

      LoginOperatorResult result = await context.Hikyaku.Send(
        new LoginOperator { UserName = UserName, Password = Password },
        CancellationToken.None);

      Assert.Equal(LoginOutcome.AccountInactive, result.Outcome);
      Assert.Equal(0, context.Db.OperatorSessions.Count());
    }

    [Fact]
    public async Task LogoutOperator_EndsTheActiveSessionAndWritesJournal()
    {
      using TradingTestContext context = CreateContext();
      context.CreateOperator(UserName, Password, true);

      LoginOperatorResult login = await context.Hikyaku.Send(
        new LoginOperator { UserName = UserName, Password = Password },
        CancellationToken.None);

      await context.Hikyaku.Send(
        new LogoutOperator { OperatorId = login.OperatorId, SessionToken = login.SessionToken },
        CancellationToken.None);

      OperatorSession session = await context.Db.OperatorSessions.FirstAsync(CancellationToken.None);
      Assert.Equal(context.Clock.GetUtcNow().UtcDateTime, session.EndedAtUtc);
      Assert.Equal("operator_logout", session.EndedReason);
      Assert.Contains(context.Db.JournalEvents, item => item.Kind == JournalEventKind.OperatorLogout);
    }

    [Fact]
    public async Task LogoutOperator_WithUnknownSession_DoesNothing()
    {
      using TradingTestContext context = CreateContext();
      context.CreateOperator(UserName, Password, true);

      await context.Hikyaku.Send(
        new LogoutOperator { OperatorId = Guid.CreateVersion7(), SessionToken = "unknown-token" },
        CancellationToken.None);

      Assert.Equal(0, context.Db.JournalEvents.Count());
    }

    [Theory]
    [InlineData("operator", "secret", true)]
    [InlineData("", "secret", false)]
    [InlineData("   ", "secret", false)]
    [InlineData("operator", "", false)]
    [InlineData("operator", "   ", false)]
    public async Task ValidateOperatorCredentialsPresence_ChecksPresenceAndFormat(string userName, string password, bool expected)
    {
      using TradingTestContext context = CreateContext();

      bool isValid = await context.Hikyaku.Send(
        new ValidateOperatorCredentialsPresence { UserName = userName, Password = password },
        CancellationToken.None);

      Assert.Equal(expected, isValid);
    }

    [Fact]
    public async Task ValidateOperatorCredentialsMatching_ForUnknownOperator_ReportsNotFoundAndStaysReadOnly()
    {
      using TradingTestContext context = CreateContext();

      OperatorCredentialsValidationResult result = await context.Hikyaku.Send(
        new ValidateOperatorCredentialsMatching { UserName = "missing", Password = Password },
        CancellationToken.None);

      Assert.False(result.IsFound);
      Assert.False(result.IsValid);
      Assert.Equal(0, context.Db.JournalEvents.Count());
      Assert.Equal(0, context.Db.OperatorSessions.Count());
    }

    [Fact]
    public async Task ValidateOperatorCredentialsMatching_WithValidPassword_ReportsValid()
    {
      using TradingTestContext context = CreateContext();
      Operator operatorEntity = context.CreateOperator(UserName, Password, true);

      OperatorCredentialsValidationResult result = await context.Hikyaku.Send(
        new ValidateOperatorCredentialsMatching { UserName = UserName, Password = Password },
        CancellationToken.None);

      Assert.True(result.IsFound);
      Assert.True(result.IsValid);
      Assert.True(result.IsActive);
      Assert.False(result.IsLockedOut);
      Assert.Equal(operatorEntity.Id, result.OperatorId);
    }

    [Fact]
    public async Task ValidateOperatorCredentialsMatching_WhenLocked_ReportsLockedOut()
    {
      using TradingTestContext context = CreateContext();
      Operator operatorEntity = context.CreateOperator(UserName, Password, true);
      operatorEntity.LockedUntilUtc = context.Clock.GetUtcNow().UtcDateTime.AddMinutes(10);
      context.Db.SaveChanges();

      OperatorCredentialsValidationResult result = await context.Hikyaku.Send(
        new ValidateOperatorCredentialsMatching { UserName = UserName, Password = Password },
        CancellationToken.None);

      Assert.True(result.IsFound);
      Assert.True(result.IsLockedOut);
      Assert.False(result.IsValid);
    }
  }
}
