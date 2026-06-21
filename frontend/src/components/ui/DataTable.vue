<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { ChevronLeft, ChevronRight, Search } from 'lucide-vue-next'
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
    /** Show a search box that filters across the listed columns' text. */
    searchable?: boolean
    searchPlaceholder?: string
    /** Column keys to search (defaults to all non-action columns). */
    searchKeys?: string[]
  }>(),
  { rowKey: 'id', loading: false, clickable: false, pageSize: 12, searchable: false },
)

const emit = defineEmits<{ rowClick: [row: Record<string, unknown>] }>()
const { t } = useI18n()

const query = ref('')

const keys = computed(
  () => props.searchKeys ?? props.columns.map((c) => c.key).filter((k) => k !== 'actions'),
)

// Rows after the text filter — the basis for pagination and counts.
const source = computed(() => {
  const q = query.value.trim().toLowerCase()
  if (!props.searchable || !q) return props.rows
  return props.rows.filter((row) =>
    keys.value.some((k) => {
      const v = row[k]
      return v != null && String(v).toLowerCase().includes(q)
    }),
  )
})

const page = ref(1)
// Snap back to page 1 whenever the data or the query changes.
watch([() => props.rows, query], () => {
  page.value = 1
})

const paginated = computed(() => props.pageSize > 0 && source.value.length > props.pageSize)
const totalPages = computed(() =>
  paginated.value ? Math.ceil(source.value.length / props.pageSize) : 1,
)
const pagedRows = computed(() => {
  if (!paginated.value) return source.value
  const start = (page.value - 1) * props.pageSize
  return source.value.slice(start, start + props.pageSize)
})
const rangeFrom = computed(() => (source.value.length === 0 ? 0 : (page.value - 1) * props.pageSize + 1))
const rangeTo = computed(() => Math.min(page.value * props.pageSize, source.value.length))

function go(delta: number) {
  page.value = Math.min(totalPages.value, Math.max(1, page.value + delta))
}
</script>

<template>
  <div class="overflow-hidden rounded-card border border-border bg-surface shadow-card">
    <div v-if="searchable" class="border-b border-border p-2.5">
      <div class="relative max-w-xs">
        <Search :size="15" class="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" />
        <input
          v-model="query"
          type="search"
          :placeholder="searchPlaceholder ?? t('common.search')"
          class="field-input h-9 w-full pl-9 text-sm"
        />
      </div>
    </div>

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
          <tr v-else-if="source.length === 0">
            <td :colspan="props.columns.length" class="px-4 py-10 text-center text-text-muted">
              {{ query ? t('common.noResults') : (props.emptyText ?? '—') }}
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
      <span>{{ t('common.showingRange', { from: rangeFrom, to: rangeTo, total: source.length }) }}</span>
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
