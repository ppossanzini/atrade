using System;
using System.Collections.Generic;
using AutoTrade.Trading.Core.Dto;
using Hikyaku;

namespace AutoTrade.Trading.Core.Query.Market
{
    /// <summary>
    /// The operator queue, most recent first. It is one query for one read condition: the queue is always
    /// the same list, and filtering belongs to the client until a second read condition exists.
    /// </summary>
    public class GetProposalQueue : IRequest<List<ProposalSummaryDto>>
    {
    }

    /// <summary>Full proposal with its legs and the gate evaluations it was routed on.</summary>
    public class GetProposalDetail : IRequest<ProposalDetailDto>
    {
        public Guid ProposalId { get; set; }
    }
}
