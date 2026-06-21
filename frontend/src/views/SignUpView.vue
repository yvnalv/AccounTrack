<script setup lang="ts">
import { reactive, ref } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import AppButton from '@/components/ui/AppButton.vue'
import ThemeToggle from '@/components/ui/ThemeToggle.vue'
import LanguageToggle from '@/components/ui/LanguageToggle.vue'

const { t } = useI18n()
const auth = useAuthStore()
const router = useRouter()

const form = reactive({
  organizationName: '',
  companyName: '',
  functionalCurrency: 'IDR',
  fullName: '',
  email: '',
  password: '',
})
const error = ref('')
const submitting = ref(false)

const inputClass =
  'h-10 w-full rounded-control border border-border bg-surface px-3 text-sm text-text outline-none focus:border-accent'

async function submit() {
  error.value = ''
  submitting.value = true
  try {
    await auth.register({
      organizationName: form.organizationName.trim(),
      companyName: form.companyName.trim(),
      functionalCurrency: form.functionalCurrency.trim().toUpperCase(),
      fullName: form.fullName.trim(),
      email: form.email.trim(),
      password: form.password,
    })
    await router.push('/')
  } catch (e) {
    error.value =
      (e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? t('register.failed')
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="grid min-h-full place-items-center bg-bg px-4 py-10">
    <div class="absolute right-4 top-4 flex items-center gap-2">
      <LanguageToggle />
      <ThemeToggle />
    </div>

    <div class="w-full max-w-[440px]">
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
        <h1 class="text-lg font-semibold text-text">{{ t('register.title') }}</h1>
        <p class="mt-1 text-sm text-text-muted">{{ t('register.subtitle') }}</p>

        <form class="mt-5 space-y-4" @submit.prevent="submit">
          <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label class="mb-1.5 block text-sm font-medium text-text">{{ t('register.organization') }}</label>
              <input v-model="form.organizationName" required :class="inputClass" :placeholder="t('register.organizationPlaceholder')" />
            </div>
            <div>
              <label class="mb-1.5 block text-sm font-medium text-text">{{ t('register.company') }}</label>
              <input v-model="form.companyName" required :class="inputClass" :placeholder="t('register.companyPlaceholder')" />
            </div>
          </div>

          <div>
            <label class="mb-1.5 block text-sm font-medium text-text">{{ t('register.currency') }}</label>
            <input v-model="form.functionalCurrency" required maxlength="3" :class="`${inputClass} uppercase`" />
            <p class="mt-1 text-xs text-text-muted">{{ t('register.currencyHint') }}</p>
          </div>

          <hr class="border-border" />

          <div>
            <label class="mb-1.5 block text-sm font-medium text-text">{{ t('register.fullName') }}</label>
            <input v-model="form.fullName" required autocomplete="name" :class="inputClass" />
          </div>
          <div>
            <label class="mb-1.5 block text-sm font-medium text-text">{{ t('register.email') }}</label>
            <input v-model="form.email" type="email" required autocomplete="username" :class="inputClass" />
          </div>
          <div>
            <label class="mb-1.5 block text-sm font-medium text-text">{{ t('register.password') }}</label>
            <input v-model="form.password" type="password" required minlength="8" autocomplete="new-password" :class="inputClass" />
            <p class="mt-1 text-xs text-text-muted">{{ t('register.passwordHint') }}</p>
          </div>

          <p v-if="error" class="text-sm text-negative">{{ error }}</p>

          <AppButton type="submit" block :disabled="submitting">
            {{ submitting ? t('register.submitting') : t('register.submit') }}
          </AppButton>
        </form>

        <p class="mt-5 text-center text-sm text-text-muted">
          {{ t('register.haveAccount') }}
          <RouterLink :to="{ name: 'login' }" class="font-medium text-accent hover:underline">
            {{ t('register.signIn') }}
          </RouterLink>
        </p>
      </div>
    </div>
  </div>
</template>
