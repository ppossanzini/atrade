import { createApp } from 'vue'
import { createPinia } from 'pinia'
import ElementPlus from 'element-plus'
import 'element-plus/dist/index.css'
import '@/assets/styles/global/tokens.less'
import '@/assets/styles/global/app.less'
import App from './App.vue'
import { i18n } from './lang'
import { router } from './router'
import { loadSettings } from './settings'
import { useSessionStore } from './stores/session'

/**
 * Runtime settings and the existing server session must both be resolved before the first
 * navigation, otherwise the route guard would decide on incomplete information.
 */
async function bootstrap(): Promise<void> {
  const app = createApp(App)

  app.use(createPinia())
  app.use(i18n)
  app.use(ElementPlus)

  await loadSettings()

  const sessionStore = useSessionStore()
  await sessionStore.initialize()

  app.use(router)
  await router.isReady()

  app.mount('#app')
}

void bootstrap()
