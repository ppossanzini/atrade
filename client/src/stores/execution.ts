import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { getHttpErrorBody, getHttpStatus } from '@/services/baseRestService'
import { executionService } from '@/services/executionService'
import { useSessionStore } from './session'

/**
 * What the server can answer about a command that sends orders. A refusal carries the reason it was refused,
 * which is the part the operator needs: the store keeps it instead of reducing every rejection to a failure.
 */
export type ExecutionMutationOutcome = 'Applied' | 'Refused' | 'NotFound' | 'Failed'

/**
 * Statuses without a final outcome yet. They are listed here rather than derived from "not completed",
 * because a blocked or failed execution is also not completed and must not be counted as running.
 */
const openStatuses: server.ExecutionStatus[] = [
  'Pending',
  'Dispatching',
  'AwaitingBroker',
  'ReconciliationRequired',
  'Compensating',
  'CompensationRequired',
]

export const useExecutionStore = defineStore('execution', () => {
  const queue = ref<server.ExecutionSummary[]>([])
  const detail = ref<server.ExecutionDetail | null>(null)
  const selectedExecutionId = ref<string | null>(null)
  const isLoading = ref(false)
  const isSaving = ref(false)
  const hasLoadFailure = ref(false)
  const lastRefusalReason = ref<string | null>(null)

  const openCount = computed(
    () => queue.value.filter((item) => openStatuses.includes(item.status)).length,
  )

  const nominalCount = computed(
    () => queue.value.filter((item) => item.status === 'CompletedNominal').length,
  )

  const partialCount = computed(
    () => queue.value.filter((item) => item.status === 'CompletedPartial').length,
  )

  const compensationCount = computed(
    () => queue.value.filter((item) => item.needsCompensation).length,
  )

  /**
   * Proposals that already produced an execution. The market view uses this to stop offering a start that
   * the server would refuse anyway; the authoritative check stays on the server.
   */
  const executedProposalIds = computed(
    () =>
      new Set(
        queue.value
          .map((item) => item.proposalId)
          .filter((proposalId): proposalId is string => proposalId !== null),
      ),
  )

  function isProposalExecuted(proposalId: string): boolean {
    return executedProposalIds.value.has(proposalId)
  }

  function reset(): void {
    queue.value = []
    detail.value = null
    selectedExecutionId.value = null
    hasLoadFailure.value = false
    lastRefusalReason.value = null
  }

  async function load(): Promise<void> {
    const sessionStore = useSessionStore()

    if (!sessionStore.isAuthenticated) {
      reset()

      return
    }

    isLoading.value = true

    try {
      queue.value = await executionService.getExecutions()
      hasLoadFailure.value = false
    } catch {
      hasLoadFailure.value = true
    }

    const selectionSurvived = queue.value.some(
      (item) => item.executionId === selectedExecutionId.value,
    )

    if (!selectionSurvived) {
      selectedExecutionId.value = null
      detail.value = null
    }

    if (selectedExecutionId.value) {
      await loadDetail(selectedExecutionId.value)
    } else {
      detail.value = null
    }

    isLoading.value = false
  }

  async function loadDetail(executionId: string): Promise<void> {
    try {
      detail.value = await executionService.getExecution(executionId)
    } catch (error) {
      detail.value = null

      if (getHttpStatus(error) !== 404) {
        hasLoadFailure.value = true
      }
    }
  }

  async function selectExecution(executionId: string): Promise<void> {
    selectedExecutionId.value = executionId

    await loadDetail(executionId)
  }

  function closeDetail(): void {
    selectedExecutionId.value = null
    detail.value = null
  }

  /**
   * Starts the execution of an approved proposal. The queue is reloaded on success so the new sequence is
   * visible without a manual refresh.
   */
  async function start(proposalId: string): Promise<ExecutionMutationOutcome> {
    isSaving.value = true
    lastRefusalReason.value = null

    try {
      const result = await executionService.start(proposalId)

      await load()

      return result.outcome === 'Applied' ? 'Applied' : 'Refused'
    } catch (error) {
      const refusal = getHttpErrorBody<server.ExecutionStartResult>(error)

      lastRefusalReason.value = refusal ? refusal.reason : null

      if (getHttpStatus(error) === 404) {
        return 'NotFound'
      }

      return getHttpStatus(error) === 409 ? 'Refused' : 'Failed'
    } finally {
      isSaving.value = false
    }
  }

  /**
   * Confirms the compensation of an execution that asked for one. The reason is mandatory on the server too,
   * so an empty one is never sent.
   */
  async function compensate(
    executionId: string,
    reason: string,
  ): Promise<ExecutionMutationOutcome> {
    isSaving.value = true
    lastRefusalReason.value = null

    try {
      await executionService.compensate(executionId, reason)

      selectedExecutionId.value = executionId
      await load()

      return 'Applied'
    } catch (error) {
      const refusal = getHttpErrorBody<server.ExecutionStartResult>(error)

      lastRefusalReason.value = refusal ? refusal.reason : null

      if (getHttpStatus(error) === 404) {
        return 'NotFound'
      }

      return getHttpStatus(error) === 409 ? 'Refused' : 'Failed'
    } finally {
      isSaving.value = false
    }
  }

  return {
    queue,
    detail,
    selectedExecutionId,
    isLoading,
    isSaving,
    hasLoadFailure,
    lastRefusalReason,
    openCount,
    nominalCount,
    partialCount,
    compensationCount,
    isProposalExecuted,
    load,
    loadDetail,
    selectExecution,
    closeDetail,
    start,
    compensate,
    reset,
  }
})
