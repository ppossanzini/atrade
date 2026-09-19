namespace AutoTrade.Trading.Core.Enums
{
  public enum JournalEventKind
  {
    OperatorLoginSucceeded = 0,
    OperatorLoginFailed = 1,
    OperatorLogout = 2,
    KillSwitchEngaged = 3,
    KillSwitchReleased = 4,
    KillSwitchEngagementBlocked = 5,
    KillSwitchReleaseBlocked = 6,
    BasketCreated = 7,
    BasketRenamed = 8,
    BasketCloned = 9,
    BasketCompositionUpdated = 10,
    BasketPolicyUpdated = 11,
    BasketVersionPublished = 12,
    BasketVersionActivated = 13,
    BasketArchived = 14,
    BasketOperationRejected = 15,
    BrokerAuthorizationStarted = 16,
    BrokerAuthorizationCompleted = 17,
    BrokerAuthorizationRejected = 18,
    BrokerAuthorizationRevoked = 19,
    BrokerAuthorizationImported = 20,
    MarketManagerModeChanged = 21,
    AnalysisStarted = 22,
    AnalysisStopped = 23,
    ProposalGenerated = 24,
    ProposalAutoApproved = 25,
    ProposalApproved = 26,
    ProposalRejected = 27,
    ProposalSuspended = 28,
    ProposalExpired = 29,
    ProposalDecisionRefused = 30
  }
}
