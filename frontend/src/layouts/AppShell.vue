<script setup lang="ts">
import { computed } from 'vue'
import { RouterView, useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { useLayoutStore } from '@/stores/layout'
import AppSidebar from '@/components/layout/AppSidebar.vue'
import AppTopbar from '@/components/layout/AppTopbar.vue'
import CommandPalette from '@/components/CommandPalette.vue'

const route = useRoute()
const auth = useAuthStore()
const layout = useLayoutStore()
const { t } = useI18n()

const firstName = computed(() => (auth.user?.fullName || '').split(/\s+/)[0] || '')

const title = computed(() => {
  if (route.name === 'dashboard') {
    return t('dashboard.greeting', { name: firstName.value })
  }
  const key = route.meta.titleKey as string | undefined
  return key ? t(key) : ''
})

const subtitle = computed(() =>
  route.name === 'dashboard' ? t('dashboard.subtitle') : undefined,
)
</script>

<template>
  <div class="flex h-full bg-bg">
    <!-- Mobile drawer backdrop -->
    <Transition
      enter-active-class="transition-opacity duration-200"
      leave-active-class="transition-opacity duration-200"
      enter-from-class="opacity-0"
      leave-to-class="opacity-0"
    >
      <div
        v-if="layout.mobileOpen"
        class="fixed inset-0 z-40 bg-black/40 lg:hidden"
        @click="layout.closeMobile()"
      />
    </Transition>

    <AppSidebar />
    <div class="flex min-w-0 flex-1 flex-col">
      <AppTopbar :title="title" :subtitle="subtitle" />
      <main class="flex-1 overflow-y-auto px-4 sm:px-6 lg:px-8 pb-8">
        <!-- Full-width: content fills the main area and reflows as the sidebar collapses/expands. -->
        <div class="mx-auto w-full max-w-[1920px]">
          <RouterView />
        </div>
      </main>
    </div>
    <CommandPalette />
  </div>
</template>
