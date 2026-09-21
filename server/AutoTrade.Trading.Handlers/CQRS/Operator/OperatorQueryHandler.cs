using System;
using System.Threading;
using System.Threading.Tasks;
using AutoTrade.Trading.Core.Dto;
using AutoTrade.Trading.Core.Query.Session;
using AutoTrade.Trading.Handlers.Model;
using Hikyaku;
using Microsoft.EntityFrameworkCore;

namespace AutoTrade.Trading.Handlers.CQRS.Operator
{
  public class OperatorQueryHandler(DB db, TimeProvider timeProvider) :
    IRequestHandler<GetCurrentSession, SessionDto>,
    IRequestHandler<GetCurrentSessionByToken, SessionDto>
  {
    public async Task<SessionDto> Handle(GetCurrentSession request, CancellationToken cancellationToken)
    {
      return await BuildActiveSessionQuery(request.SessionToken)
        .Where(session => session.OperatorId == request.OperatorId)
        .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SessionDto> Handle(GetCurrentSessionByToken request, CancellationToken cancellationToken)
    {
      return await BuildActiveSessionQuery(request.SessionToken).FirstOrDefaultAsync(cancellationToken);
    }

    private IQueryable<SessionDto> BuildActiveSessionQuery(string sessionToken)
    {
      DateTime now = timeProvider.GetUtcNow().UtcDateTime;

      return from operatorSession in db.OperatorSessions
                                  join operatorEntity in db.Operators on operatorSession.OperatorId equals operatorEntity.Id
                                  where operatorSession.SessionToken == sessionToken
                                        && operatorSession.EndedAtUtc == null
                                        && operatorSession.ExpiresAtUtc > now
                                        && operatorEntity.IsActive
                                  select new SessionDto
                                  {
                                    SessionId = operatorSession.Id,
                                    OperatorId = operatorEntity.Id,
                                    UserName = operatorEntity.UserName,
                                    StartedAtUtc = operatorSession.StartedAtUtc,
                                    ExpiresAtUtc = operatorSession.ExpiresAtUtc
                                  };
    }
  }
}
