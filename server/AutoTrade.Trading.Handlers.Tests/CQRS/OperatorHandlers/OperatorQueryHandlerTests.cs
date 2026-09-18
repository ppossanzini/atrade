using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Command.Session;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Query.Session;
using AutoTrade.Trading.Handlers.Model;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.CQRS.OperatorHandlers
{
  public class OperatorQueryHandlerTests
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
    public async Task GetCurrentSession_WithActiveSession_ReturnsProjectedSession()
    {
      using TradingTestContext context = CreateContext();
      Operator operatorEntity = context.CreateOperator(UserName, Password, true);

      LoginOperatorResult login = await context.Hikyaku.Send(
        new LoginOperator { UserName = UserName, Password = Password },
        CancellationToken.None);

      SessionDto session = await context.Hikyaku.Send(
        new GetCurrentSession { OperatorId = login.OperatorId, SessionToken = login.SessionToken },
        CancellationToken.None);

      Assert.NotNull(session);
      Assert.Equal(login.SessionId, session.SessionId);
      Assert.Equal(operatorEntity.Id, session.OperatorId);
      Assert.Equal(UserName, session.UserName);
      Assert.Equal(login.ExpiresAtUtc, session.ExpiresAtUtc);
    }

    [Fact]
    public async Task GetCurrentSession_WithUnknownToken_ReturnsNull()
    {
      using TradingTestContext context = CreateContext();
      context.CreateOperator(UserName, Password, true);

      LoginOperatorResult login = await context.Hikyaku.Send(
        new LoginOperator { UserName = UserName, Password = Password },
        CancellationToken.None);

      SessionDto session = await context.Hikyaku.Send(
        new GetCurrentSession { OperatorId = login.OperatorId, SessionToken = "not-the-token" },
        CancellationToken.None);

      Assert.Null(session);
    }

    [Fact]
    public async Task GetCurrentSession_AfterLogout_ReturnsNull()
    {
      using TradingTestContext context = CreateContext();
      context.CreateOperator(UserName, Password, true);

      LoginOperatorResult login = await context.Hikyaku.Send(
        new LoginOperator { UserName = UserName, Password = Password },
        CancellationToken.None);

      await context.Hikyaku.Send(
        new LogoutOperator { OperatorId = login.OperatorId, SessionToken = login.SessionToken },
        CancellationToken.None);

      SessionDto session = await context.Hikyaku.Send(
        new GetCurrentSession { OperatorId = login.OperatorId, SessionToken = login.SessionToken },
        CancellationToken.None);

      Assert.Null(session);
    }

    [Fact]
    public async Task GetCurrentSession_WhenSessionExpired_ReturnsNull()
    {
      using TradingTestContext context = CreateContext();
      context.CreateOperator(UserName, Password, true);

      LoginOperatorResult login = await context.Hikyaku.Send(
        new LoginOperator { UserName = UserName, Password = Password },
        CancellationToken.None);

      context.Clock.Advance(TimeSpan.FromMinutes(241));

      SessionDto session = await context.Hikyaku.Send(
        new GetCurrentSession { OperatorId = login.OperatorId, SessionToken = login.SessionToken },
        CancellationToken.None);

      Assert.Null(session);
    }

    [Fact]
    public async Task GetCurrentSession_WhenOperatorIsInactive_ReturnsNull()
    {
      using TradingTestContext context = CreateContext();
      Operator operatorEntity = context.CreateOperator(UserName, Password, true);

      LoginOperatorResult login = await context.Hikyaku.Send(
        new LoginOperator { UserName = UserName, Password = Password },
        CancellationToken.None);

      operatorEntity.IsActive = false;
      context.Db.SaveChanges();

      SessionDto session = await context.Hikyaku.Send(
        new GetCurrentSession { OperatorId = login.OperatorId, SessionToken = login.SessionToken },
        CancellationToken.None);

      Assert.Null(session);
    }
  }
}
