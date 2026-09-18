import { computed, defineComponent, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useSessionStore } from '@/stores/session'

export default defineComponent({
  name: 'TradingShell',
  setup() {
    const { t } = useI18n()
    const route = useRoute()
    const router = useRouter()
    const sessionStore = useSessionStore()

    const navigationItems = [
      { routeName: 'status', labelKey: 'navigation.status' },
      { routeName: 'basket', labelKey: 'navigation.basket' },
      { routeName: 'strategy', labelKey: 'navigation.strategy' },
      { routeName: 'market', labelKey: 'navigation.market' },
      { routeName: 'execution', labelKey: 'navigation.execution' },
      { routeName: 'history', labelKey: 'navigation.history' },
      { routeName: 'journal', labelKey: 'navigation.journal' },
    ]

    const activeRouteName = computed(() => String(route.name ?? 'status'))

    // The shell owns navigation: a revoked or expired session returns the operator to the login.
    watch(
      () => sessionStore.isAuthenticated,
      (isAuthenticated) => {
        if (!isAuthenticated) {
          void router.push({ name: 'login' })
        }
      },
    )

    async function signOut(): Promise<void> {
      await sessionStore.logout()
      await router.push({ name: 'login' })
    }

    return {
      t,
      sessionStore,
      navigationItems,
      activeRouteName,
      signOut,
    }
  },
})
