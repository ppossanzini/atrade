import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { basketService } from '@/services/basketService'
import { getHttpStatus } from '@/services/baseRestService'
import { useSessionStore } from './session'

/**
 * Outcome vocabulary for basket mutations, keyed to the server semantics: 400 is invalid input,
 * 409 is a conflict or an invalid state and 404 is a missing basket. The views choose the wording
 * per operation, because a 409 means a duplicate name for create and a non-archivable basket
 * for archive.
 */
export type BasketMutationOutcome = 'Applied' | 'InvalidInput' | 'Conflict' | 'NotFound' | 'Failed'

function toOutcome(error: unknown): BasketMutationOutcome {
  const status = getHttpStatus(error)

  if (status === 400) {
    return 'InvalidInput'
  }

  if (status === 404) {
    return 'NotFound'
  }

  if (status === 409) {
    return 'Conflict'
  }

  return 'Failed'
}

export const useBasketsStore = defineStore('baskets', () => {
  const baskets = ref<server.BasketSummary[]>([])
  const selectedBasketId = ref<string | null>(null)
  const detail = ref<server.BasketDetail | null>(null)
  const versions = ref<server.BasketVersion[]>([])
  const activeBasket = ref<server.BasketDetail | null>(null)
  const isLoading = ref(false)
  const isSaving = ref(false)
  const hasLoadFailure = ref(false)

  const selectedSummary = computed(
    () => baskets.value.find((item) => item.basketId === selectedBasketId.value) ?? null,
  )

  const isSelectedArchived = computed(() => detail.value?.status === 'Archived')

  async function loadRegistry(): Promise<void> {
    const sessionStore = useSessionStore()

    if (!sessionStore.isAuthenticated) {
      reset()

      return
    }

    isLoading.value = true

    try {
      const registry = await basketService.getBaskets()
      baskets.value = registry
      await loadActiveBasket()
      hasLoadFailure.value = false

      const selectionSurvived = registry.some((item) => item.basketId === selectedBasketId.value)

      if (!selectionSurvived) {
        selectedBasketId.value = registry.length > 0 ? (registry[0]?.basketId ?? null) : null
        detail.value = null
        versions.value = []
      }

      if (selectedBasketId.value) {
        await loadSelection()
      }
    } catch {
      reset()
      hasLoadFailure.value = true
    } finally {
      isLoading.value = false
    }
  }

  function reset(): void {
    baskets.value = []
    selectedBasketId.value = null
    detail.value = null
    versions.value = []
    activeBasket.value = null
  }

  async function selectBasket(basketId: string): Promise<void> {
    selectedBasketId.value = basketId

    await loadSelection()
  }

  async function loadSelection(): Promise<void> {
    if (!selectedBasketId.value) {
      detail.value = null
      versions.value = []

      return
    }

    isLoading.value = true

    try {
      detail.value = await basketService.getBasketById(selectedBasketId.value)
      versions.value = await basketService.getVersions(selectedBasketId.value)
      hasLoadFailure.value = false
    } catch (error) {
      detail.value = null
      versions.value = []
      hasLoadFailure.value = getHttpStatus(error) !== 404
    } finally {
      isLoading.value = false
    }
  }

  async function loadActiveBasket(): Promise<void> {
    try {
      activeBasket.value = await basketService.getActiveBasket()
    } catch {
      // Having no active version is a normal state, not a failure.
      activeBasket.value = null
    }
  }

  async function createBasket(name: string): Promise<BasketMutationOutcome> {
    return runMutation(() => basketService.createBasket({ name }), true)
  }

  async function renameSelectedBasket(name: string): Promise<BasketMutationOutcome> {
    if (!selectedBasketId.value) {
      return 'Failed'
    }

    const basketId = selectedBasketId.value

    return runMutation(() => basketService.updateIdentity(basketId, { name }), false)
  }

  async function cloneSelectedBasket(name: string): Promise<BasketMutationOutcome> {
    if (!selectedBasketId.value) {
      return 'Failed'
    }

    const basketId = selectedBasketId.value

    return runMutation(() => basketService.cloneBasket(basketId, { name }), true)
  }

  async function saveComposition(
    legs: server.BasketCompositionLeg[],
  ): Promise<BasketMutationOutcome> {
    if (!selectedBasketId.value) {
      return 'Failed'
    }

    const basketId = selectedBasketId.value

    return runMutation(() => basketService.updateComposition(basketId, legs), false)
  }

  async function savePolicy(policy: server.BasketPolicy): Promise<BasketMutationOutcome> {
    if (!selectedBasketId.value) {
      return 'Failed'
    }

    const basketId = selectedBasketId.value

    return runMutation(() => basketService.updatePolicy(basketId, policy), false)
  }

  async function publishVersion(note: string): Promise<BasketMutationOutcome> {
    if (!selectedBasketId.value) {
      return 'Failed'
    }

    const basketId = selectedBasketId.value

    return runMutation(() => basketService.publishVersion(basketId, { note }), false)
  }

  async function activateVersion(versionId: string): Promise<BasketMutationOutcome> {
    if (!selectedBasketId.value) {
      return 'Failed'
    }

    const basketId = selectedBasketId.value

    return runMutation(() => basketService.activateVersion(basketId, versionId), false)
  }

  async function archiveSelectedBasket(): Promise<BasketMutationOutcome> {
    if (!selectedBasketId.value) {
      return 'Failed'
    }

    const basketId = selectedBasketId.value

    return runMutation(() => basketService.archiveBasket(basketId), false)
  }

  /**
   * Runs one write through the shared transaction ceremony: request the antiforgery token, send the
   * single contract of the scope, then refresh the registry, the detail and the version history so
   * the UI never shows a state the server did not confirm.
   */
  async function runMutation(
    action: () => Promise<server.BasketDetail>,
    selectResult: boolean,
  ): Promise<BasketMutationOutcome> {
    const sessionStore = useSessionStore()
    await sessionStore.ensureCsrfToken()

    isSaving.value = true

    try {
      const updated = await action()

      if (selectResult) {
        selectedBasketId.value = updated.basketId
      }

      await loadRegistry()

      return 'Applied'
    } catch (error) {
      return toOutcome(error)
    } finally {
      isSaving.value = false
    }
  }

  return {
    baskets,
    selectedBasketId,
    detail,
    versions,
    activeBasket,
    isLoading,
    isSaving,
    hasLoadFailure,
    selectedSummary,
    isSelectedArchived,
    loadRegistry,
    selectBasket,
    loadSelection,
    createBasket,
    renameSelectedBasket,
    cloneSelectedBasket,
    saveComposition,
    savePolicy,
    publishVersion,
    activateVersion,
    archiveSelectedBasket,
  }
})
