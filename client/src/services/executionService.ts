import { BaseRestService } from './baseRestService'

/**
 * Transport-only access to the execution endpoints. Starting an execution and confirming a compensation both
 * send orders, so both are attributed to the operator authenticated by the shared Bearer transport.
 */
export class ExecutionService extends BaseRestService {
  constructor() {
    super('/api/execution')
  }

  async getExecutions(): Promise<server.ExecutionSummary[]> {
    return this.get<server.ExecutionSummary[]>('/executions')
  }

  async getExecution(executionId: string): Promise<server.ExecutionDetail> {
    return this.get<server.ExecutionDetail>(`/executions/${executionId}`)
  }

  async start(proposalId: string): Promise<server.ExecutionStartResult> {
    return this.post<server.ExecutionStartResult>(`/proposals/${proposalId}/start`, {})
  }

  async compensate(executionId: string, reason: string): Promise<server.ExecutionStartResult> {
    return this.post<server.ExecutionStartResult>(`/executions/${executionId}/compensate`, {
      reason,
    })
  }
}

export const executionService = new ExecutionService()
