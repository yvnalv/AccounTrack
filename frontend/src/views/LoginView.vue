<script setup lang="ts">
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import AppButton from '@/components/ui/AppButton.vue'
import ThemeToggle from '@/components/ui/ThemeToggle.vue'
import LanguageToggle from '@/components/ui/LanguageToggle.vue'

const { t } = useI18n()
const auth = useAuthStore()
const router = useRouter()
const route = useRoute()

const email = ref('admin@accountrack.local')
const password = ref('')
const error = ref('')
const submitting = ref(false)

async function submit() {
  error.value = ''
  submitting.value = true
  try {
    await auth.login(email.value, password.value)
    const redirect = (route.query.redirect as string) || '/'
    await router.push(redirect)
  } catch {
    error.value = t('login.failed')
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="grid min-h-full place-items-center bg-bg px-4">
    <div class="absolute right-4 top-4 flex items-center gap-2">
      <LanguageToggle />
      <ThemeToggle />
    </div>

    <div class="w-full max-w-[400px]">
      <div class="mb-6 flex items-center gap-2.5">
        <svg width="36" height="36" viewBox="0 0 40 40" fill="none" aria-hidden="true">
          <rect width="40" height="40" rx="11" fill="#007E6E" />
          <g fill="#FFFFFF">
            <rect x="11" y="21" width="5" height="8" rx="2" />
            <rect x="17.5" y="16" width="5" height="13" rx="2" />
            <rect x="24" y="11" width="5" height="18" rx="2" />
          </g>
        </svg>
        <span class="text-xl font-bold tracking-tight text-text">
          Accoun<span class="text-accent">track</span>
        </span>
      </div>

      <div class="rounded-card border border-border bg-surface p-6 shadow-card">
        <h1 class="text-lg font-semibold text-text">{{ t('login.title') }}</h1>
        <p class="mt-1 text-sm text-text-muted">{{ t('login.subtitle') }}</p>

        <form class="mt-5 space-y-4" @submit.prevent="submit">
          <div>
            <label class="mb-1.5 block text-sm font-medium text-text" for="email">
              {{ t('login.email') }}
            </label>
            <input
              id="email"
              v-model="email"
              type="email"
              autocomplete="username"
              required
              class="h-10 w-full rounded-control border border-border bg-surface px-3 text-sm text-text outline-none focus:border-accent"
            />
          </div>
          <div>
            <label class="mb-1.5 block text-sm font-medium text-text" for="password">
              {{ t('login.password') }}
            </label>
            <input
              id="password"
              v-model="password"
              type="password"
              autocomplete="current-password"
              required
              class="h-10 w-full rounded-control border border-border bg-surface px-3 text-sm text-text outline-none focus:border-accent"
            />
          </div>

          <p v-if="error" class="text-sm text-negative">{{ error }}</p>

          <AppButton type="submit" block :disabled="submitting">
            {{ submitting ? t('login.signingIn') : t('login.submit') }}
          </AppButton>
        </form>
      </div>
    </div>
  </div>
</template>
