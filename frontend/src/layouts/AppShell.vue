<script setup lang="ts">
import { computed } from 'vue'
import { RouterView, useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import AppSidebar from '@/components/layout/AppSidebar.vue'
import AppTopbar from '@/components/layout/AppTopbar.vue'
import CommandPalette from '@/components/CommandPalette.vue'

const route = useRoute()
const auth = useAuthStore()
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
    <AppSidebar />
    <div class="flex min-w-0 flex-1 flex-col">
      <AppTopbar :title="title" :subtitle="subtitle" />
      <main class="flex-1 overflow-y-auto px-6 lg:px-8 pb-8">
        <!-- Left-aligned cap: content hugs the sidebar at the same gutter as the top bar; any
             extra width on very wide screens falls to the right, not as a left gap. -->
        <div class="max-w-[1600px]">
          <RouterView />
        </div>
      </main>
    </div>
    <CommandPalette />
  </div>
</template>
