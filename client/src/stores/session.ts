import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { getHttpStatus, setAccessTokenProvider } from '@/services/baseRestService'
import { sessionService } from '@/services/sessionService'

export type LoginOutcome =
  | 'success'
  | 'missingFields'
  | 'invalidCredentials'
  | 'lockedOut'
  | 'accountInactive'
  | 'unexpectedError'

/**
 * Server-side validated session. The cookie is only a carrier: the authoritative session lives on
 * the server, so the store re-reads it instead of trusting local state.
 */
export const useSessionStore = defineStore('session', () => {
  const accessTokenStorageKey = 'autotrade.accessToken'
  const currentSession = ref<server.Session | null>(null)
  const accessToken = ref<string | null>(sessionStorage.getItem(accessTokenStorageKey))
  const isBusy = ref(false)
  const isInitialized = ref(false)

  const isAuthenticated = computed(() => currentSession.value !== null)
  const operatorName = computed(() => currentSession.value?.userName ?? '')

  setAccessTokenProvider(() => accessToken.value)

  function clearSession(): void {
    accessToken.value = null
    currentSession.value = null
    sessionStorage.removeItem(accessTokenStorageKey)
  }

  /**
   * Reads the current session. A failure leaves the operator unauthenticated rather than assuming
   * access, and the login attempt surfaces any connectivity problem.
   */
  async function initialize(): Promise<void> {
    try {
      currentSession.value = await sessionService.getCurrent()
    } catch {
      clearSession()
    } finally {
      isInitialized.value = true
    }
  }

  async function login(credentials: server.LoginRequest): Promise<LoginOutcome> {
    if (!credentials.userName.trim() || !credentials.password) {
      return 'missingFields'
    }

    isBusy.value = true

    try {
      const authenticatedSession = await sessionService.login(credentials)
      accessToken.value = authenticatedSession.accessToken
      currentSession.value = authenticatedSession.session
      sessionStorage.setItem(accessTokenStorageKey, authenticatedSession.accessToken)
      return 'success'
    } catch (error) {
      const status = getHttpStatus(error)

      if (status === 401) {
        return 'invalidCredentials'
      }

      if (status === 423) {
        return 'lockedOut'
      }

      if (status === 403) {
        return 'accountInactive'
      }

      return 'unexpectedError'
    } finally {
      isBusy.value = false
    }
  }

  async function logout(): Promise<void> {
    isBusy.value = true

    try {
      await sessionService.logout()
    } finally {
      clearSession()
      isBusy.value = false
    }
  }

  return {
    currentSession,
    isBusy,
    isInitialized,
    isAuthenticated,
    operatorName,
    initialize,
    login,
    logout,
  }
})
