<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { Column } from './types'

const props = withDefaults(
  defineProps<{
    columns: Column[]
    rows: Record<string, unknown>[]
    rowKey?: string
    loading?: boolean
    emptyText?: string
    clickable?: boolean
  }>(),
  { rowKey: 'id', loading: false, clickable: false },
)

const emit = defineEmits<{ rowClick: [row: Record<string, unknown>] }>()
const { t } = useI18n()
</script>

<template>
  <div class="overflow-x-auto rounded-card border border-border bg-surface shadow-card">
    <table class="w-full text-sm">
      <thead>
        <tr class="border-b border-border text-text-muted">
          <th
            v-for="col in props.columns"
            :key="col.key"
            class="bg-surface-2 px-4 py-2.5 text-xs font-semibold uppercase tracking-wide"
            :class="col.align === 'right' ? 'text-right' : 'text-left'"
          >
            {{ col.label }}
          </th>
        </tr>
      </thead>
      <tbody>
        <tr v-if="props.loading">
          <td :colspan="props.columns.length" class="px-4 py-8 text-center text-text-muted">
            {{ t('common.loading') }}
          </td>
        </tr>
        <tr v-else-if="props.rows.length === 0">
          <td :colspan="props.columns.length" class="px-4 py-10 text-center text-text-muted">
            {{ props.emptyText ?? '—' }}
          </td>
        </tr>
        <tr
          v-for="row in props.rows"
          v-else
          :key="String(row[props.rowKey])"
          class="border-b border-border last:border-0 transition-colors"
          :class="props.clickable ? 'cursor-pointer hover:bg-surface-2' : ''"
          @click="props.clickable && emit('rowClick', row)"
        >
          <td
            v-for="col in props.columns"
            :key="col.key"
            class="px-4 py-3 text-text"
            :class="[col.align === 'right' ? 'text-right' : 'text-left', col.numeric ? 'tnum' : '']"
          >
            <slot :name="`cell-${col.key}`" :row="row" :value="row[col.key]">
              {{ row[col.key] }}
            </slot>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>
