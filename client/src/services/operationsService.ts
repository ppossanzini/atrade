import { BaseRestService } from './baseRestService'

export class OperationsService extends BaseRestService {
  constructor() {
    super('/api/operations')
  }

  async getStatus(): Promise<server.OperationalStatus> {
    return this.get<server.OperationalStatus>('/status')
  }

  async engageKillSwitch(request: server.KillSwitchEngagement): Promise<server.KillSwitchChange> {
    return this.post<server.KillSwitchChange>('/kill-switch/engage', request)
  }

  async releaseKillSwitch(): Promise<server.KillSwitchChange> {
    return this.post<server.KillSwitchChange>('/kill-switch/release', {})
  }
}

export const operationsService = new OperationsService()
