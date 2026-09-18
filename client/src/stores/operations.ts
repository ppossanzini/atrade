import { ref } from 'vue'
import { defineStore } from 'pinia'
import { operationsService } from '@/services/operationsService'
import { useSessionStore } from './session'

export const useOperationsStore = defineStore('operations', () => {
  const status = ref<server.OperationalStatus | null>(null)
  const isLoading = ref(false)
  const hasLoadFailure = ref(false)

  async function loadStatus(): Promise<void> {
    const sessionStore = useSessionStore()

    if (!sessionStore.isAuthenticated) {
      status.value = null

      return
    }

    isLoading.value = true

    try {
      status.value = await operationsService.getStatus()
      hasLoadFailure.value = false
    } catch {
      status.value = null
      hasLoadFailure.value = true
    } finally {
      isLoading.value = false
    }
  }

  async function engageKillSwitch(reason: string): Promise<server.KillSwitchChangeOutcome | null> {
    const sessionStore = useSessionStore()
    await sessionStore.ensureCsrfToken()

    try {
      const change = await operationsService.engageKillSwitch({ reason })
      await loadStatus()

      return change.outcome
    } catch {
      return null
    }
  }

  async function releaseKillSwitch(): Promise<server.KillSwitchChangeOutcome | null> {
    const sessionStore = useSessionStore()
    await sessionStore.ensureCsrfToken()

    try {
      const change = await operationsService.releaseKillSwitch()
      await loadStatus()

      return change.outcome
    } catch {
      return null
    }
  }

  return {
    status,
    isLoading,
    hasLoadFailure,
    loadStatus,
    engageKillSwitch,
    releaseKillSwitch,
  }
})
