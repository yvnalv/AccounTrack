<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { ImportPreview } from '@/types/masterdata'
import AppButton from '@/components/ui/AppButton.vue'
import AppModal from '@/components/ui/AppModal.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'

defineProps<{
  modelValue: boolean
  title: string
  preview: ImportPreview | null
  busy: boolean
  error: string
  canCommit: boolean
}>()
const emit = defineEmits<{ 'update:modelValue': [boolean]; confirm: [] }>()

const { t } = useI18n()

function tone(action: string) {
  return action === 'Error' ? 'negative' : action === 'Update' ? 'info' : 'positive'
}
</script>

<template>
  <AppModal :model-value="modelValue" :title="title" @update:model-value="emit('update:modelValue', $event)">
    <div class="space-y-3">
      <p v-if="busy && !preview" class="text-sm text-text-muted">{{ t('common.loading') }}</p>
      <template v-else-if="preview">
        <div class="flex flex-wrap gap-4 text-sm">
          <span class="text-text-muted">{{ t('masterData.import.rows') }}: <b class="text-text tnum">{{ preview.totalRows }}</b></span>
          <span class="text-positive">{{ t('masterData.import.create') }}: <b class="tnum">{{ preview.toCreate }}</b></span>
          <span class="text-info">{{ t('masterData.import.update') }}: <b class="tnum">{{ preview.toUpdate }}</b></span>
          <span :class="preview.errorRows ? 'text-negative' : 'text-text-muted'">{{ t('masterData.import.errors') }}: <b class="tnum">{{ preview.errorRows }}</b></span>
        </div>
        <div class="max-h-72 overflow-y-auto rounded-card border border-border">
          <table class="w-full text-sm">
            <thead>
              <tr class="border-b border-border bg-surface-2 text-xs uppercase tracking-wide text-text-muted">
                <th class="px-3 py-2 text-left font-semibold">#</th>
                <th class="px-3 py-2 text-left font-semibold">{{ t('masterData.fields.code') }}</th>
                <th class="px-3 py-2 text-left font-semibold">{{ t('masterData.import.action') }}</th>
                <th class="px-3 py-2 text-left font-semibold">{{ t('masterData.import.issues') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="r in preview.rows" :key="r.rowNumber" class="border-b border-border last:border-0">
                <td class="px-3 py-1.5 text-text-muted tnum">{{ r.rowNumber }}</td>
                <td class="px-3 py-1.5 text-text">{{ r.key || '—' }}</td>
                <td class="px-3 py-1.5">
                  <StatusBadge :label="t(`masterData.import.actions.${r.action}`)" :tone="tone(r.action)" />
                </td>
                <td class="px-3 py-1.5 text-negative">{{ r.errors.join(' ') }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <p v-if="preview.errorRows" class="text-xs text-text-muted">{{ t('masterData.import.allOrNothing') }}</p>
      </template>
      <p v-if="error" class="text-sm text-negative">{{ error }}</p>
    </div>
    <template #footer>
      <AppButton variant="ghost" @click="emit('update:modelValue', false)">{{ t('masterData.cancel') }}</AppButton>
      <AppButton :disabled="busy || !canCommit" @click="emit('confirm')">
        {{ busy ? t('masterData.saving') : t('masterData.import.confirm') }}
      </AppButton>
    </template>
  </AppModal>
</template>
