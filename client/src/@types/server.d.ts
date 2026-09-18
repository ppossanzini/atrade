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
}
