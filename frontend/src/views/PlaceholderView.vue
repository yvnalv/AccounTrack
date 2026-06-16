<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { Construction } from 'lucide-vue-next'

const { t } = useI18n()
</script>

<template>
  <div class="grid place-items-center py-24">
    <div class="flex max-w-sm flex-col items-center text-center">
      <div class="grid h-12 w-12 place-items-center rounded-card bg-accent-soft text-accent">
        <Construction :size="24" />
      </div>
      <h2 class="mt-4 text-base font-semibold text-text">{{ t('common.comingSoon') }}</h2>
      <p class="mt-1 text-sm text-text-muted">{{ t('common.comingSoonBody') }}</p>
    </div>
  </div>
</template>
