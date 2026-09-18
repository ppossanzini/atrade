import { defineComponent, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { ElMessage } from 'element-plus'
import { useSessionStore, type LoginOutcome } from '@/stores/session'

const outcomeMessageKeys: Record<LoginOutcome, string> = {
  success: '',
  missingFields: 'auth.missingFields',
  invalidCredentials: 'auth.invalidCredentials',
  lockedOut: 'auth.lockedOut',
  accountInactive: 'auth.accountInactive',
  unexpectedError: 'auth.unexpectedError',
}

export default defineComponent({
  name: 'LoginView',
  setup() {
    const { t } = useI18n()
    const router = useRouter()
    const sessionStore = useSessionStore()

    const credentials = ref<server.LoginRequest>({ userName: '', password: '' })

    async function submit(): Promise<void> {
      const outcome = await sessionStore.login(credentials.value)

      if (outcome === 'success') {
        await router.push({ name: 'status' })

        return
      }

      ElMessage.error(t(outcomeMessageKeys[outcome]))
    }

    return { t, credentials, sessionStore, submit }
  },
})
