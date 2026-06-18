import { createI18n } from 'vue-i18n'
import en from './locales/en'
import id from './locales/id'

export type Locale = 'en' | 'id'
const STORAGE_KEY = 'accountrack.locale'

export function savedLocale(): Locale {
  const v = localStorage.getItem(STORAGE_KEY)
  return v === 'id' ? 'id' : 'en' // English default (CLAUDE.md); Indonesian supported
}

export function persistLocale(locale: Locale): void {
  localStorage.setItem(STORAGE_KEY, locale)
}

// English default; Bahasa Indonesia supported (CLAUDE.md i18n requirement).
export const i18n = createI18n({
  legacy: false,
  locale: savedLocale(),
  fallbackLocale: 'en',
  messages: { en, id },
})

export type MessageSchema = typeof en
