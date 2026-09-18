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
  public class OperatorQueryHandler(DB db, TimeProvider timeProvider) : IRequestHandler<GetCurrentSession, SessionDto>
  {
    public async Task<SessionDto> Handle(GetCurrentSession request, CancellationToken cancellationToken)
    {
      DateTime now = timeProvider.GetUtcNow().UtcDateTime;

      SessionDto session = await (from operatorSession in db.OperatorSessions
                                  join operatorEntity in db.Operators on operatorSession.OperatorId equals operatorEntity.Id
                                  where operatorSession.SessionToken == request.SessionToken
                                        && operatorSession.OperatorId == request.OperatorId
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
                                  }).FirstOrDefaultAsync(cancellationToken);

      return session;
    }
  }
}
