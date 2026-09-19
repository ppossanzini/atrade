import { BaseRestService } from './baseRestService'

/**
 * Transport-only access to the risk endpoints. The gate decision and the configured thresholds are
 * read-only resources: nothing here mutates server state.
 */
export class RiskService extends BaseRestService {
  constructor() {
    super('/api/risk')
  }

  async getLimits(): Promise<server.RiskLimits> {
    return this.get<server.RiskLimits>('/limits')
  }

  async getBasketDecision(basketId: string): Promise<server.RiskDecision> {
    return this.get<server.RiskDecision>(`/baskets/${basketId}`)
  }
}

export const riskService = new RiskService()
