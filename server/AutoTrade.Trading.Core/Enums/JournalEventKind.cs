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
    BasketOperationRejected = 15
  }
}
