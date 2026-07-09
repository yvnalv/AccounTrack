<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { RouterLink } from 'vue-router'
import { ShieldOff } from 'lucide-vue-next'

const { t } = useI18n()
</script>

<template>
  <div class="grid place-items-center py-24">
    <div class="flex max-w-sm flex-col items-center text-center">
      <div class="grid h-12 w-12 place-items-center rounded-card bg-negative/10 text-negative">
        <ShieldOff :size="24" />
      </div>
      <h2 class="mt-4 text-base font-semibold text-text">{{ t('forbidden.title') }}</h2>
      <p class="mt-1 text-sm text-text-muted">{{ t('forbidden.body') }}</p>
      <RouterLink
        :to="{ name: 'dashboard' }"
        class="mt-5 inline-flex items-center rounded-control bg-accent px-4 h-10 text-sm font-medium text-accent-contrast transition-colors hover:opacity-90"
      >
        {{ t('forbidden.back') }}
      </RouterLink>
    </div>
  </div>
</template>
