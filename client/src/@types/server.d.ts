declare namespace server {
  type IsoDateTime = string

  type TradingEnvironment = 'Demo' | 'Live'

  type BrokerConnectionState = 'Disconnected' | 'Connected' | 'Degraded' | 'ReconciliationRequired'

  type MarketManagerMode = 'Manual' | 'Supervised' | 'Automatic'

  type KillSwitchChangeOutcome = 'Applied' | 'Blocked'

  interface Session {
    sessionId: string
    operatorId: string
    userName: string
    startedAtUtc: IsoDateTime
    expiresAtUtc: IsoDateTime
  }

  interface LoginRequest {
    userName: string
    password: string
  }

  interface AntiforgeryToken {
    token: string
  }

  interface KillSwitchStatus {
    isEngaged: boolean
    changedAtUtc: IsoDateTime | null
    changedByOperatorId: string | null
    reason: string | null
  }

  interface TradingAccountStatus {
    brokerAccountId: number
    environment: TradingEnvironment
    connectionState: BrokerConnectionState
    isTradingEnabled: boolean
    lastBrokerSyncUtc: IsoDateTime | null
    lastReconciledUtc: IsoDateTime | null
  }

  interface MarketManagerStatus {
    mode: MarketManagerMode
    isAnalysisRunning: boolean
    lastCycleAtUtc: IsoDateTime | null
  }

  interface OperationalStatus {
    serverTimeUtc: IsoDateTime
    killSwitch: KillSwitchStatus
    account: TradingAccountStatus | null
    marketManager: MarketManagerStatus | null
  }

  interface KillSwitchChange {
    outcome: KillSwitchChangeOutcome
    isEngaged: boolean
    changedAtUtc: IsoDateTime
  }

  interface KillSwitchEngagement {
    reason: string
  }

  type BasketStatus = 'Active' | 'Inactive' | 'Archived'

  type BasketVersionStatus = 'Published' | 'Active' | 'Superseded' | 'Archived'

  type LegDirection = 'Long' | 'Short'

  type MarketKind = 'Fx' | 'Metal' | 'Index'

  type TimeFrame = 'M5' | 'M15' | 'M30' | 'H1'

  type FailurePolicy = 'MinimumCoverage' | 'AllOrNothing' | 'RequireConfirmation'

  /**
   * Operator-owned leg definition. Analysis values are produced by the analysis pipeline and are
   * deliberately absent from this contract.
   */
  interface BasketCompositionLeg {
    symbol: string
    market: MarketKind
    direction: LegDirection
    timeFrame: TimeFrame
    weight: number
    riskCap: number
    /** Stop distance of the leg in pips: a decision about this instrument inside this basket. */
    stopDistancePips: number
    isSelected: boolean
  }

  interface BasketPolicy {
    failurePolicy: FailurePolicy
    minimumCoverage: number
    riskPerBasket: number
    dailyLossLimit: number
  }

  interface BasketSummary {
    basketId: string
    name: string
    status: BasketStatus
    activeVersionNumber: number
    latestVersionNumber: number
    selectedLegCount: number
    totalWeight: number
    updatedAtUtc: IsoDateTime
  }

  interface BasketDetail {
    basketId: string
    name: string
    status: BasketStatus
    latestVersionNumber: number
    activeVersionId: string | null
    activeVersionNumber: number
    draftLegs: BasketCompositionLeg[]
    draftPolicy: BasketPolicy | null
  }

  interface BasketVersion {
    versionId: string
    number: number
    status: BasketVersionStatus
    note: string
    createdAtUtc: IsoDateTime
    publishedAtUtc: IsoDateTime | null
  }

  interface BasketIdentity {
    name: string
  }

  interface BasketVersionPublication {
    note: string
  }

  type RiskGateVerdict = 'Allow' | 'Review' | 'Block'

  /**
   * Stable gate codes. The names are part of the audit contract, so a code is never reused for a
   * different rule and the client never invents a label for an unknown one.
   */
  type RiskGateCode =
    | 'ActiveVersionMissing'
    | 'KillSwitchEngaged'
    | 'SnapshotMissing'
    | 'SnapshotStale'
    | 'CoverageBelowMinimum'
    | 'RiskPerBasketExceeded'
    | 'DailyLossExceeded'
    | 'LegSpreadExceeded'
    | 'LegVolatilityExceeded'
    | 'LegDataMissing'
    | 'BasketDataMissing'
    | 'ThresholdNotConfigured'

  /**
   * One evaluated gate. Code, observed value, threshold and timestamp are always present as fields;
   * a missing measurement is null, never an omitted property.
   */
  interface RiskGateResult {
    code: RiskGateCode
    verdict: RiskGateVerdict
    subject: string
    market: MarketKind | null
    observedValue: number | null
    thresholdValue: number | null
    unit: string | null
    evaluatedAtUtc: IsoDateTime
    detail: string
  }

  interface RiskDecision {
    verdict: RiskGateVerdict
    evaluatedAtUtc: IsoDateTime
    basketId: string | null
    basketVersionId: string | null
    versionNumber: number
    snapshotCapturedAtUtc: IsoDateTime | null
    gates: RiskGateResult[]
  }

  interface MarketRiskLimits {
    market: MarketKind
    legSpreadMaxPips: number | null
    legVolatilityMaxPercent: number | null
    isConfigured: boolean
  }

  interface RiskLimits {
    snapshotMaxAgeSeconds: number | null
    markets: MarketRiskLimits[]
    isFullyConfigured: boolean
  }

  type ProposalAction = 'Entry' | 'Reduce' | 'Exit'

  type ProposalStatus =
    'NeedsReview' | 'AutoApproved' | 'Blocked' | 'Approved' | 'Rejected' | 'Suspended' | 'Expired'

  type ProposalDecisionOutcome =
    'Applied' | 'NotFound' | 'NotDecidable' | 'Expired' | 'GateRegressed' | 'AlreadyDecided'

  /**
   * One row of the operator queue. Decidability is computed by the server for the current mode, so the
   * client never infers it from the status alone.
   */
  interface ProposalSummary {
    proposalId: string
    basketId: string
    basketName: string
    versionNumber: number
    action: ProposalAction
    status: ProposalStatus
    gate: RiskGateVerdict
    confidence: number
    expectedRiskPercent: number
    proposedAtUtc: IsoDateTime
    expiresAtUtc: IsoDateTime
    isDecidable: boolean
  }

  interface ProposalLeg {
    symbol: string
    market: MarketKind
    direction: LegDirection
    weight: number
    riskCap: number
    stopDistancePips: number
  }

  interface ProposalDetail extends ProposalSummary {
    snapshotId: string | null
    snapshotCapturedAtUtc: IsoDateTime | null
    cycleSequence: number
    rationale: string
    decidedAtUtc: IsoDateTime | null
    decidedByOperatorId: string | null
    decisionReason: string | null
    legs: ProposalLeg[]
    gates: RiskGateResult[]
  }

  interface ProposalDecisionResult {
    proposalId: string
    outcome: ProposalDecisionOutcome
    status: ProposalStatus
    decidedAtUtc: IsoDateTime
  }
}
