import { ref } from 'vue'
import { defineStore } from 'pinia'
import { getHttpStatus } from '@/services/baseRestService'
import { riskService } from '@/services/riskService'
import { useSessionStore } from './session'

/**
 * Risk state shared across the operator surface. The decision is produced by the server from stored
 * state, so the store never derives a verdict locally and never caches a decision for another basket.
 */
export const useRiskStore = defineStore('risk', () => {
  const decision = ref<server.RiskDecision | null>(null)
  const limits = ref<server.RiskLimits | null>(null)
  const isLoading = ref(false)
  const hasLoadFailure = ref(false)

  /**
   * Loads the thresholds in force and, when a basket is selected, its current decision. The two
   * resources fail independently: a missing basket (404) is a normal state and not a load failure,
   * and an unreadable threshold list never hides a decision that was read.
   */
  async function load(basketId: string | null): Promise<void> {
    const sessionStore = useSessionStore()

    if (!sessionStore.isAuthenticated) {
      reset()

      return
    }

    isLoading.value = true
    hasLoadFailure.value = false

    try {
      limits.value = await riskService.getLimits()
    } catch {
      limits.value = null
      hasLoadFailure.value = true
    }

    try {
      decision.value = basketId ? await riskService.getBasketDecision(basketId) : null
    } catch (error) {
      decision.value = null
      hasLoadFailure.value = hasLoadFailure.value || getHttpStatus(error) !== 404
    } finally {
      isLoading.value = false
    }
  }

  function reset(): void {
    decision.value = null
    limits.value = null
    hasLoadFailure.value = false
  }

  return {
    decision,
    limits,
    isLoading,
    hasLoadFailure,
    load,
    reset,
  }
})
