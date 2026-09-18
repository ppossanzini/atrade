import { createApp } from 'vue'
import ElementPlus from 'element-plus'
import 'element-plus/dist/index.css'
import * as ElementPlusIconsVue from '@element-plus/icons-vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import i18n from './lang'
import router from './router'
import './assets/styles/global/app.less'

const app = createApp(App)

for (const [name, component] of Object.entries(ElementPlusIconsVue)) {
	app.component(name, component)
}

app.use(createPinia())
app.use(router)
app.use(i18n)
app.use(ElementPlus)
app.mount('#app')
