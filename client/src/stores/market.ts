import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { getHttpStatus } from '@/services/baseRestService'
import { marketService } from '@/services/marketService'
import { useSessionStore } from './session'

/**
 * Decision vocabulary of the Market Manager, mirroring what the server can answer. The view turns these
 * into copy: the store only reports what happened, so an explanation is never invented here.
 */
export type ProposalDecisionKind = 'approve' | 'reject' | 'suspend'

export type MarketMutationOutcome =
  | 'Applied'
  | 'Refused'
  | 'NotDecidable'
  | 'Expired'
  | 'GateRegressed'
  | 'AlreadyDecided'
  | 'NotFound'
  | 'Failed'

function toOutcome(error: unknown): MarketMutationOutcome {
  const status = getHttpStatus(error)

  if (status === 404) {
    return 'NotFound'
  }

  return 'Failed'
}

export const useMarketStore = defineStore('market', () => {
  const manager = ref<server.MarketManagerStatus | null>(null)
  const proposals = ref<server.ProposalSummary[]>([])
  const detail = ref<server.ProposalDetail | null>(null)
  const selectedProposalId = ref<string | null>(null)
  const isLoading = ref(false)
  const isSaving = ref(false)
  const hasLoadFailure = ref(false)

  const decidableProposals = computed(() => proposals.value.filter((item) => item.isDecidable))

  const attentionCount = computed(
    () => proposals.value.filter((item) => item.status === 'NeedsReview').length,
  )

  const autoApprovedCount = computed(
    () => proposals.value.filter((item) => item.status === 'AutoApproved').length,
  )

  const blockedCount = computed(
    () => proposals.value.filter((item) => item.status === 'Blocked').length,
  )

  /**
   * Loads the operating state, the queue and the detail of the selection. The three reads fail
   * independently so a missing detail never hides the queue the operator is working on.
   */
  async function load(): Promise<void> {
    const sessionStore = useSessionStore()

    if (!sessionStore.isAuthenticated) {
      reset()

      return
    }

    isLoading.value = true
    hasLoadFailure.value = false

    try {
      manager.value = await marketService.getManager()
      proposals.value = await marketService.getProposals()
      hasLoadFailure.value = false
    } catch {
      hasLoadFailure.value = true
    }

    const selectionSurvived = proposals.value.some(
      (item) => item.proposalId === selectedProposalId.value,
    )

    if (!selectionSurvived) {
      selectedProposalId.value = null
      detail.value = null
    }

    if (selectedProposalId.value) {
      await loadDetail(selectedProposalId.value)
    } else {
      detail.value = null
    }

    isLoading.value = false
  }

  async function loadDetail(proposalId: string): Promise<void> {
    try {
      detail.value = await marketService.getProposal(proposalId)
    } catch (error) {
      detail.value = null

      if (getHttpStatus(error) !== 404) {
        hasLoadFailure.value = true
      }
    }
  }

  async function selectProposal(proposalId: string): Promise<void> {
    selectedProposalId.value = proposalId

    await loadDetail(proposalId)
  }

  function closeDetail(): void {
    selectedProposalId.value = null
    detail.value = null
  }

  function reset(): void {
    manager.value = null
    proposals.value = []
    detail.value = null
    selectedProposalId.value = null
    hasLoadFailure.value = false
  }

  async function setMode(mode: server.MarketManagerMode): Promise<MarketMutationOutcome> {
    isSaving.value = true

    try {
      await marketService.setMode(mode)
      await load()

      return 'Applied'
    } catch (error) {
      return toOutcome(error)
    } finally {
      isSaving.value = false
    }
  }

  async function setAnalysisState(isRunning: boolean): Promise<MarketMutationOutcome> {
    isSaving.value = true

    try {
      await marketService.setAnalysisState(isRunning)
      await load()

      return 'Applied'
    } catch (error) {
      // A refused switch is a conflict: the conditions for starting are not met, and the view explains
      // which ones instead of pretending the analysis started.
      return getHttpStatus(error) === 409 ? 'Refused' : toOutcome(error)
    } finally {
      isSaving.value = false
    }
  }

  async function decide(
    kind: ProposalDecisionKind,
    proposalId: string,
    reason: string,
  ): Promise<MarketMutationOutcome> {
    isSaving.value = true

    try {
      const result =
        kind === 'approve'
          ? await marketService.approve(proposalId)
          : kind === 'reject'
            ? await marketService.reject(proposalId, reason)
            : await marketService.suspend(proposalId, reason)

      await load()

      return result.outcome
    } catch (error) {
      const status = getHttpStatus(error)

      // 400 is a missing reason, 409 a refused decision: both are reported as refusals, never as success.
      return status === 400 ? 'Refused' : toOutcome(error)
    } finally {
      isSaving.value = false
    }
  }

  return {
    manager,
    proposals,
    detail,
    selectedProposalId,
    isLoading,
    isSaving,
    hasLoadFailure,
    decidableProposals,
    attentionCount,
    autoApprovedCount,
    blockedCount,
    load,
    selectProposal,
    closeDetail,
    setMode,
    setAnalysisState,
    decide,
    reset,
  }
})
