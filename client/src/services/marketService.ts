import { BaseRestService } from './baseRestService'

/**
 * Transport-only access to the Market Manager endpoints. Decisions are attributed to the operator on the
 * server, so no identity travels in the payload; the shared transport adds the Bearer credential.
 */
export class MarketService extends BaseRestService {
  constructor() {
    super('/api/market')
  }

  async getManager(): Promise<server.MarketManagerStatus> {
    return this.get<server.MarketManagerStatus>('/manager')
  }

  async setMode(mode: server.MarketManagerMode): Promise<server.MarketManagerMode> {
    return this.put<server.MarketManagerMode>('/manager/mode', { mode })
  }

  async setAnalysisState(isRunning: boolean): Promise<void> {
    await this.put<void>('/manager/analysis', { isRunning })
  }

  async getProposals(): Promise<server.ProposalSummary[]> {
    return this.get<server.ProposalSummary[]>('/proposals')
  }

  async getProposal(proposalId: string): Promise<server.ProposalDetail> {
    return this.get<server.ProposalDetail>(`/proposals/${proposalId}`)
  }

  async approve(proposalId: string): Promise<server.ProposalDecisionResult> {
    return this.post<server.ProposalDecisionResult>(`/proposals/${proposalId}/approve`, {})
  }

  async reject(proposalId: string, reason: string): Promise<server.ProposalDecisionResult> {
    return this.post<server.ProposalDecisionResult>(`/proposals/${proposalId}/reject`, { reason })
  }

  async suspend(proposalId: string, reason: string): Promise<server.ProposalDecisionResult> {
    return this.post<server.ProposalDecisionResult>(`/proposals/${proposalId}/suspend`, { reason })
  }
}

export const marketService = new MarketService()
