import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { getHttpStatus, setRequestHeaderProvider } from '@/services/baseRestService'
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
  const currentSession = ref<server.Session | null>(null)
  const csrfToken = ref<string | null>(null)
  const isBusy = ref(false)
  const isInitialized = ref(false)

  const isAuthenticated = computed(() => currentSession.value !== null)
  const operatorName = computed(() => currentSession.value?.userName ?? '')

  setRequestHeaderProvider((): Record<string, string> =>
    csrfToken.value ? { 'X-CSRF-TOKEN': csrfToken.value } : {},
  )

  async function ensureCsrfToken(force = false): Promise<void> {
    if (csrfToken.value && !force) {
      return
    }

    const token = await sessionService.getAntiforgeryToken()
    csrfToken.value = token.token
  }

  async function refreshCsrfTokenQuietly(): Promise<void> {
    try {
      await ensureCsrfToken(true)
    } catch {
      // The token is optional until a mutating call needs it; that call reports the failure.
      csrfToken.value = null
    }
  }

  /**
   * Reads the current session. A failure leaves the operator unauthenticated rather than assuming
   * access, and the login attempt surfaces any connectivity problem.
   */
  async function initialize(): Promise<void> {
    try {
      currentSession.value = await sessionService.getCurrent()
    } catch {
      currentSession.value = null
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
      await ensureCsrfToken()
      currentSession.value = await sessionService.login(credentials)

      // Antiforgery tokens are bound to the caller identity, so the anonymous token obtained
      // before authentication cannot be used for authenticated mutations.
      await refreshCsrfTokenQuietly()

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

      if (status === 400) {
        // The antiforgery token no longer matches its cookie: renew it for the next attempt.
        await refreshCsrfTokenQuietly()

        return 'invalidCredentials'
      }

      return 'unexpectedError'
    } finally {
      isBusy.value = false
    }
  }

  async function logout(): Promise<void> {
    isBusy.value = true

    try {
      await ensureCsrfToken()
      await sessionService.logout()
    } finally {
      currentSession.value = null
      isBusy.value = false
      await refreshCsrfTokenQuietly()
    }
  }

  return {
    currentSession,
    csrfToken,
    isBusy,
    isInitialized,
    isAuthenticated,
    operatorName,
    ensureCsrfToken,
    initialize,
    login,
    logout,
  }
})
