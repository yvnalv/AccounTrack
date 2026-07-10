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
      class="grid h-8 w-8 place-items-center rounded-md text-text-muted transition-colors hover:bg-surface-2 hover:text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
      :title="t('masterData.edit')"
      :aria-label="t('masterData.edit')"
      @click="$emit('edit')"
    >
      <Pencil :size="15" />
    </button>
    <button
      v-if="auth.has('MasterData.Delete')"
      type="button"
      class="grid h-8 w-8 place-items-center rounded-md transition-colors hover:bg-surface-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
      :class="props.row.isActive ? 'text-negative' : 'text-positive'"
      :title="props.row.isActive ? t('masterData.deactivate') : t('masterData.activate')"
      :aria-label="props.row.isActive ? t('masterData.deactivate') : t('masterData.activate')"
      @click="$emit('toggle')"
    >
      <component :is="props.row.isActive ? Ban : RotateCcw" :size="15" />
    </button>
    <span v-if="!auth.has('MasterData.Edit') && !auth.has('MasterData.Delete')" class="text-xs text-text-muted">—</span>
  </div>
</template>
