import { createI18n } from 'vue-i18n'
import en from './locales/en'

// English now; Bahasa Indonesia is added later (CLAUDE.md i18n requirement).
export const i18n = createI18n({
  legacy: false,
  locale: 'en',
  fallbackLocale: 'en',
  messages: { en },
})

export type MessageSchema = typeof en
