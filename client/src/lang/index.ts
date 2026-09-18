import { createI18n } from 'vue-i18n'
import it from './it'

export const i18n = createI18n({
  legacy: false,
  locale: 'it',
  fallbackLocale: 'it',
  messages: { it },
})
