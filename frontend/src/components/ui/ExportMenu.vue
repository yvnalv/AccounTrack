<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { Download, ChevronDown } from 'lucide-vue-next'
import type { ExportFormat } from '@/lib/api'

const props = defineProps<{ download: (format: ExportFormat) => void | Promise<void> }>()

const { t } = useI18n()
const open = ref(false)
const root = ref<HTMLElement | null>(null)

function choose(format: ExportFormat) {
  open.value = false
  props.download(format)
}

function onClickOutside(e: MouseEvent) {
  if (root.value && !root.value.contains(e.target as Node)) open.value = false
}
onMounted(() => document.addEventListener('click', onClickOutside))
onBeforeUnmount(() => document.removeEventListener('click', onClickOutside))
</script>

<template>
  <div ref="root" class="relative">
    <button
      class="inline-flex items-center gap-1.5 rounded-button border border-border px-3 py-2 text-sm font-medium text-text-muted transition-colors hover:bg-surface-2 hover:text-text"
      @click="open = !open"
    >
      <Download :size="16" /> {{ t('common.export') }} <ChevronDown :size="14" />
    </button>
    <div
      v-if="open"
      class="absolute right-0 z-20 mt-1 w-40 overflow-hidden rounded-card border border-border bg-surface shadow-card"
    >
      <button class="block w-full px-3 py-2 text-left text-sm text-text hover:bg-surface-2" @click="choose('xlsx')">
        {{ t('common.excel') }}
      </button>
      <button class="block w-full px-3 py-2 text-left text-sm text-text hover:bg-surface-2" @click="choose('csv')">
        {{ t('common.csv') }}
      </button>
    </div>
  </div>
</template>
