<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { Pencil, Ban, RotateCcw } from 'lucide-vue-next'

const props = defineProps<{ row: { isActive: boolean } }>()
defineEmits<{ edit: []; toggle: [] }>()

const { t } = useI18n()
</script>

<template>
  <div class="flex items-center justify-end gap-1">
    <button
      type="button"
      class="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium text-text-muted transition-colors hover:bg-surface-2 hover:text-text"
      @click="$emit('edit')"
    >
      <Pencil :size="14" /> {{ t('masterData.edit') }}
    </button>
    <button
      type="button"
      class="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium transition-colors hover:bg-surface-2"
      :class="props.row.isActive ? 'text-negative' : 'text-positive'"
      @click="$emit('toggle')"
    >
      <component :is="props.row.isActive ? Ban : RotateCcw" :size="14" />
      {{ props.row.isActive ? t('masterData.deactivate') : t('masterData.activate') }}
    </button>
  </div>
</template>
