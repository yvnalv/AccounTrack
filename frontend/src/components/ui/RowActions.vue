<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { Pencil, Ban, RotateCcw } from 'lucide-vue-next'
import { useAuthStore } from '@/stores/auth'

const props = defineProps<{ row: { isActive: boolean } }>()
defineEmits<{ edit: []; toggle: [] }>()

const { t } = useI18n()
const auth = useAuthStore()
</script>

<template>
  <div class="flex items-center justify-end gap-1">
    <button
      v-if="auth.has('MasterData.Edit')"
      type="button"
      class="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium text-text-muted transition-colors hover:bg-surface-2 hover:text-text"
      @click="$emit('edit')"
    >
      <Pencil :size="14" /> {{ t('masterData.edit') }}
    </button>
    <button
      v-if="auth.has('MasterData.Delete')"
      type="button"
      class="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium transition-colors hover:bg-surface-2"
      :class="props.row.isActive ? 'text-negative' : 'text-positive'"
      @click="$emit('toggle')"
    >
      <component :is="props.row.isActive ? Ban : RotateCcw" :size="14" />
      {{ props.row.isActive ? t('masterData.deactivate') : t('masterData.activate') }}
    </button>
    <span v-if="!auth.has('MasterData.Edit') && !auth.has('MasterData.Delete')" class="text-xs text-text-muted">—</span>
  </div>
</template>
