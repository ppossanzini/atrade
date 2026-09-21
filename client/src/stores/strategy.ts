import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { getHttpStatus } from '@/services/baseRestService'
import { basketService } from '@/services/basketService'
import { operationsService } from '@/services/operationsService'
import { useBasketsStore } from './baskets'
import { useSessionStore } from './session'

export type StrategyMutationOutcome = 'Applied' | 'Refused' | 'NotFound' | 'Failed'

/**
 * The strategy of the basket in force. There is no separate strategy entity: the rules the operator
 * configures here are the policy of the basket version, frozen on publication. Keeping one source is the
 * point — a second copy would let the screen and the risk gate disagree about the same limit.
 */
export const useStrategyStore = defineStore('strategy', () => {
  const basketsStore = useBasketsStore()

  const promotion = ref<server.PromotionStatus | null>(null)
  const isLoading = ref(false)
  const isSaving = ref(false)
  const hasLoadFailure = ref(false)

  /** Rules in force: what the active version froze, not what the draft says. */
  const rulesInForce = computed<server.BasketPolicy | null>(
    () => basketsStore.activeBasket?.activePolicy ?? null,
  )

  /** Rules in preparation: the draft that becomes the next version on publication. */
  const rulesInPreparation = computed<server.BasketPolicy | null>(
    () => basketsStore.activeBasket?.draftPolicy ?? null,
  )

  const activeVersionNumber = computed(() => basketsStore.activeBasket?.activeVersionNumber ?? 0)

  /** True when the draft differs from what is in force, so the screen can say a publication is pending. */
  const hasPendingChanges = computed(() => {
    const inForce = rulesInForce.value
    const draft = rulesInPreparation.value

    if (!inForce || !draft) {
      return false
    }

    return (
      inForce.entryMode !== draft.entryMode ||
      inForce.failurePolicy !== draft.failurePolicy ||
      inForce.minimumCoverage !== draft.minimumCoverage ||
      inForce.riskPerBasket !== draft.riskPerBasket ||
      inForce.dailyLossLimit !== draft.dailyLossLimit
    )
  })

  async function load(): Promise<void> {
    const sessionStore = useSessionStore()

    if (!sessionStore.isAuthenticated) {
      reset()

      return
    }

    isLoading.value = true

    try {
      await basketsStore.loadActiveBasket()
      promotion.value = await operationsService.getPromotion()
      hasLoadFailure.value = false
    } catch {
      hasLoadFailure.value = true
    }

    isLoading.value = false
  }

  /**
   * Saves the draft rules. The version in force does not change here: the operator publishes it on purpose
   * from the basket, which is why the screen keeps the two sets of values apart.
   */
  async function saveRules(policy: server.BasketPolicy): Promise<StrategyMutationOutcome> {
    const basketId = basketsStore.activeBasket?.basketId

    if (!basketId) {
      return 'NotFound'
    }

    isSaving.value = true

    try {
      await basketService.updatePolicy(basketId, policy)
      await load()

      return 'Applied'
    } catch (error) {
      const status = getHttpStatus(error)

      return status === 400 || status === 409 ? 'Refused' : status === 404 ? 'NotFound' : 'Failed'
    } finally {
      isSaving.value = false
    }
  }

  function reset(): void {
    promotion.value = null
    hasLoadFailure.value = false
  }

  return {
    promotion,
    isLoading,
    isSaving,
    hasLoadFailure,
    rulesInForce,
    rulesInPreparation,
    activeVersionNumber,
    hasPendingChanges,
    load,
    saveRules,
    reset,
  }
})
