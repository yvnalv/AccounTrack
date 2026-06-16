<script setup lang="ts">
import { Moon, Sun } from 'lucide-vue-next'
import { useThemeStore } from '@/stores/theme'
import { useI18n } from 'vue-i18n'

const theme = useThemeStore()
const { t } = useI18n()
</script>

<template>
  <button
    type="button"
    class="grid h-9 w-9 place-items-center rounded-full border border-border bg-surface text-text-muted transition-colors hover:text-text hover:bg-surface-2"
    :title="t('theme.toggle')"
    :aria-label="t('theme.toggle')"
    @click="theme.toggle()"
  >
    <Moon v-if="theme.theme === 'light'" :size="18" />
    <Sun v-else :size="18" />
  </button>
</template>
