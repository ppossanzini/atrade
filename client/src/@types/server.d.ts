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
    evidenceStore: EvidenceStoreStatus | null
  }

  /** Semantic memory as the application sees it. Unavailable degrades retrieval, never authority. */
  interface EvidenceStoreStatus {
    provider: string
    isAvailable: boolean
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

  /** Declared entry rule of the strategy. It is a declaration the version freezes and the proposal records. */
  type EntryMode = 'RegimeMomentum' | 'Momentum' | 'MeanReversion'

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
    /**
     * Widest spread tolerated on this leg, in pips, and highest volatility tolerated, as a percentage.
     * Zero means the operator has not decided yet, and the risk gate blocks that leg until it is decided.
     */
    maxSpreadPips: number
    maxVolatilityPercent: number
    isSelected: boolean
  }

  interface BasketPolicy {
    entryMode: EntryMode
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
    /**
     * Policy frozen in the active version, when this basket is the one holding it. The draft is what is being
     * prepared; these are the rules actually in force, and they change only on publication.
     */
    activePolicy: BasketPolicy | null
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

  interface RiskLimits {
    snapshotMaxAgeSeconds: number | null
    isConfigured: boolean
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
    /** Entry rule the strategy was following when the proposal was produced. */
    entryMode: EntryMode
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
    /** Spread and volatility limits frozen with the version. Zero means not decided. */
    maxSpreadPips: number
    maxVolatilityPercent: number
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

  type ExecutionStatus =
    | 'Pending'
    | 'Dispatching'
    | 'AwaitingBroker'
    | 'ReconciliationRequired'
    | 'CompensationRequired'
    | 'Compensating'
    | 'CompletedNominal'
    | 'CompletedPartial'
    | 'Blocked'
    | 'Failed'

  type ExecutionLegStatus =
    | 'Pending'
    | 'Dispatched'
    | 'Accepted'
    | 'PartiallyFilled'
    | 'Filled'
    | 'Rejected'
    | 'TimedOut'
    | 'ReconciliationRequired'
    | 'Compensated'

  type ExecutionOutcome =
    | 'Applied'
    | 'NotFound'
    | 'NotAuthorized'
    | 'AlreadyExecuted'
    | 'NotConfigured'
    | 'Blocked'
    | 'Conflict'

  type ExecutionEventKind =
    | 'Unknown'
    | 'OrderAccepted'
    | 'OrderRejected'
    | 'OrderPartiallyFilled'
    | 'OrderFilled'
    | 'OrderCancelled'

  /**
   * One row of the execution queue. Coverage is measured by the server from the volumes really filled, and
   * the compensation need is read, never inferred from the status alone.
   */
  interface ExecutionSummary {
    executionId: string
    proposalId: string | null
    basketId: string
    basketName: string
    versionNumber: number
    status: ExecutionStatus
    failurePolicy: FailurePolicy
    minimumCoverage: number
    coverage: number
    legCount: number
    filledLegCount: number
    createdAtUtc: IsoDateTime
    completedAtUtc: IsoDateTime | null
    needsCompensation: boolean
  }

  interface ExecutionLeg {
    legId: string
    ordinal: number
    symbol: string
    market: MarketKind
    direction: LegDirection
    volumeUnits: number
    filledVolumeUnits: number
    clientOrderId: string | null
    brokerOrderId: string | null
    status: ExecutionLegStatus
    averagePrice: number | null
    errorCode: string | null
    lastEventAtUtc: IsoDateTime | null
  }

  interface ExecutionEvent {
    brokerEventId: string
    kind: ExecutionEventKind
    symbol: string | null
    payload: string | null
    receivedAtUtc: IsoDateTime
  }

  interface ExecutionDetail extends ExecutionSummary {
    snapshotId: string | null
    compensationOfExecutionId: string | null
    startedAtUtc: IsoDateTime | null
    legs: ExecutionLeg[]
    events: ExecutionEvent[]
  }

  interface ExecutionStartResult {
    executionId: string
    outcome: ExecutionOutcome
    status: ExecutionStatus
    reason: string | null
  }

  /**
   * Whether one requirement of the live promotion gate has been met. `NotVerifiable` means the application
   * cannot decide it, which is not the same as it being met or unmet.
   */
  type PromotionRequirementState = 'Satisfied' | 'NotSatisfied' | 'NotVerifiable'

  interface PromotionRequirement {
    key: string
    state: PromotionRequirementState
    evidence: string | null
  }

  interface PromotionStatus {
    isLiveEligible: boolean
    currentEnvironment: string | null
    requirements: PromotionRequirement[]
  }
}
