import { BaseRestService } from './baseRestService'

export class OperationsService extends BaseRestService {
  constructor() {
    super('/api/operations')
  }

  async getStatus(): Promise<server.OperationalStatus> {
    return this.get<server.OperationalStatus>('/status')
  }

  /**
   * State of the live promotion gate. Read-only: the gate is closed by a human decision, so there is no
   * matching command here and there is not meant to be one.
   */
  async getPromotion(): Promise<server.PromotionStatus> {
    return this.get<server.PromotionStatus>('/promotion')
  }

  async engageKillSwitch(request: server.KillSwitchEngagement): Promise<server.KillSwitchChange> {
    return this.post<server.KillSwitchChange>('/kill-switch/engage', request)
  }

  async releaseKillSwitch(): Promise<server.KillSwitchChange> {
    return this.post<server.KillSwitchChange>('/kill-switch/release', {})
  }
}

export const operationsService = new OperationsService()
