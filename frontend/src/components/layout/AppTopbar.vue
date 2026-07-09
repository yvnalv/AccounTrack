<script setup lang="ts">
import { computed } from 'vue'
import { LogOut, Menu } from 'lucide-vue-next'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { useLayoutStore } from '@/stores/layout'
import ThemeToggle from '@/components/ui/ThemeToggle.vue'
import LanguageToggle from '@/components/ui/LanguageToggle.vue'
import NotificationBell from '@/components/layout/NotificationBell.vue'

defineProps<{ title: string; subtitle?: string }>()

const auth = useAuthStore()
const layout = useLayoutStore()
const router = useRouter()
const { t } = useI18n()

const initials = computed(() => {
  const name = auth.user?.fullName || auth.user?.email || '?'
  return name
    .split(/\s+/)
    .slice(0, 2)
    .map((p) => p[0]?.toUpperCase() ?? '')
    .join('')
})

async function signOut() {
  // Must await: logout() clears the session only after revoking server-side. Navigating before it
  // resolves would leave the user "authenticated", and the guard would bounce /login back to the
  // dashboard. Clearing first means the guard lets the redirect through.
  await auth.logout()
  void router.push({ name: 'login' })
}
</script>

<template>
  <header class="flex items-center justify-between gap-4 px-6 lg:px-8 py-4">
    <div class="flex min-w-0 items-center gap-3">
      <button
        type="button"
        class="grid h-9 w-9 shrink-0 place-items-center rounded-control border border-border bg-surface text-text-muted transition-colors hover:text-text hover:bg-surface-2 lg:hidden"
        :aria-label="t('nav.menu')"
        @click="layout.toggleMobile()"
      >
        <Menu :size="18" />
      </button>
      <div class="min-w-0">
        <h1 class="truncate text-xl font-semibold text-text">{{ title }}</h1>
        <p v-if="subtitle" class="truncate text-sm text-text-muted">{{ subtitle }}</p>
      </div>
    </div>

    <div class="flex items-center gap-2">
      <LanguageToggle />
      <ThemeToggle />
      <NotificationBell />

      <div class="ml-1 flex items-center gap-3 border-l border-border pl-3">
        <div class="hidden text-right leading-tight sm:block">
          <p class="text-sm font-medium text-text">{{ auth.user?.fullName }}</p>
          <p class="text-xs text-text-muted">{{ auth.user?.email }}</p>
        </div>
        <div class="grid h-9 w-9 place-items-center rounded-full bg-accent text-sm font-semibold text-accent-contrast">
          {{ initials }}
        </div>
        <button
          type="button"
          class="grid h-9 w-9 place-items-center rounded-full text-text-muted transition-colors hover:text-text hover:bg-surface-2"
          :title="t('common.signOut')"
          :aria-label="t('common.signOut')"
          @click="signOut"
        >
          <LogOut :size="18" />
        </button>
      </div>
    </div>
  </header>
</template>
