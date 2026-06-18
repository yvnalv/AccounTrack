<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { Languages } from 'lucide-vue-next'
import { persistLocale, type Locale } from '@/i18n'

const { locale, t } = useI18n()

function toggle() {
  const next: Locale = locale.value === 'id' ? 'en' : 'id'
  locale.value = next
  persistLocale(next)
}
</script>

<template>
  <button
    type="button"
    class="flex h-9 items-center gap-1.5 rounded-full border border-border bg-surface px-3 text-xs font-semibold text-text-muted transition-colors hover:text-text hover:bg-surface-2"
    :title="t('common.language')"
    :aria-label="t('common.language')"
    @click="toggle"
  >
    <Languages :size="16" />
    {{ locale.toUpperCase() }}
  </button>
</template>
