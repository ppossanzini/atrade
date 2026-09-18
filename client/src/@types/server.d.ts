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
}
