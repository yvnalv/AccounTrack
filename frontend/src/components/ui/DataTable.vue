<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { ChevronLeft, ChevronRight } from 'lucide-vue-next'
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
    /** Rows per page; 0 disables pagination. */
    pageSize?: number
  }>(),
  { rowKey: 'id', loading: false, clickable: false, pageSize: 12 },
)

const emit = defineEmits<{ rowClick: [row: Record<string, unknown>] }>()
const { t } = useI18n()

const page = ref(1)
// Snap back to the first page whenever the underlying data changes (filter, reload, …).
watch(
  () => props.rows,
  () => {
    page.value = 1
  },
)

const paginated = computed(() => props.pageSize > 0 && props.rows.length > props.pageSize)
const totalPages = computed(() =>
  paginated.value ? Math.ceil(props.rows.length / props.pageSize) : 1,
)
const pagedRows = computed(() => {
  if (!paginated.value) return props.rows
  const start = (page.value - 1) * props.pageSize
  return props.rows.slice(start, start + props.pageSize)
})
const rangeFrom = computed(() => (props.rows.length === 0 ? 0 : (page.value - 1) * props.pageSize + 1))
const rangeTo = computed(() => Math.min(page.value * props.pageSize, props.rows.length))

function go(delta: number) {
  page.value = Math.min(totalPages.value, Math.max(1, page.value + delta))
}
</script>

<template>
  <div class="overflow-hidden rounded-card border border-border bg-surface shadow-card">
    <div class="overflow-x-auto">
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
            v-for="row in pagedRows"
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

    <div
      v-if="paginated && !loading"
      class="flex items-center justify-between border-t border-border px-4 py-2.5 text-xs text-text-muted"
    >
      <span>{{ t('common.showingRange', { from: rangeFrom, to: rangeTo, total: props.rows.length }) }}</span>
      <div class="flex items-center gap-1">
        <button
          class="grid h-7 w-7 place-items-center rounded-control transition-colors hover:bg-surface-2 hover:text-text disabled:opacity-40 disabled:hover:bg-transparent"
          :disabled="page <= 1"
          :aria-label="t('common.prev')"
          @click="go(-1)"
        >
          <ChevronLeft :size="16" />
        </button>
        <span class="px-1 tabular-nums">{{ page }} / {{ totalPages }}</span>
        <button
          class="grid h-7 w-7 place-items-center rounded-control transition-colors hover:bg-surface-2 hover:text-text disabled:opacity-40 disabled:hover:bg-transparent"
          :disabled="page >= totalPages"
          :aria-label="t('common.next')"
          @click="go(1)"
        >
          <ChevronRight :size="16" />
        </button>
      </div>
    </div>
  </div>
</template>
