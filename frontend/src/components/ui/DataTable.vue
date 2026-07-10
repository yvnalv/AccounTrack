<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight, ChevronsUpDown, Search, X } from 'lucide-vue-next'
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
    /** True when a parent-owned filter (status/category/…) is set, so the Clear button shows and the
     *  parent can reset those filters via the `clear` event. */
    filtersActive?: boolean
  }>(),
  { rowKey: 'id', loading: false, clickable: false, pageSize: 12, searchable: false, filtersActive: false },
)

const emit = defineEmits<{ rowClick: [row: Record<string, unknown>]; clear: [] }>()
// Two-way exposure of the currently-filtered rows (all pages), so a parent can export exactly what
// the user is seeing after search/filter (used by Export — see lib/exportTable.ts).
const filtered = defineModel<Record<string, unknown>[]>('filtered', { default: () => [] })
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

// --- Column sorting (click a header: asc → desc → off). On by default for every column but `actions`.
const sortKey = ref<string | null>(null)
const sortDir = ref<'asc' | 'desc'>('asc')

function isSortable(col: Column): boolean {
  return col.sortable !== false && col.key !== 'actions'
}

function sortValueOf(col: Column, row: Record<string, unknown>): unknown {
  return col.sortValue ? col.sortValue(row) : row[col.key]
}

function toggleSort(col: Column) {
  if (!isSortable(col)) return
  if (sortKey.value !== col.key) {
    sortKey.value = col.key
    sortDir.value = 'asc'
  } else if (sortDir.value === 'asc') {
    sortDir.value = 'desc'
  } else {
    sortKey.value = null // third click restores the natural order
  }
}

function compare(a: unknown, b: unknown): number {
  if (a == null && b == null) return 0
  if (a == null) return 1 // nulls/blanks sort last
  if (b == null) return -1
  if (typeof a === 'number' && typeof b === 'number') return a - b
  // numeric-aware, case-insensitive text compare (so "10" sorts after "9", "item2" after "item10" no)
  return String(a).localeCompare(String(b), undefined, { numeric: true, sensitivity: 'base' })
}

const sorted = computed(() => {
  const key = sortKey.value
  if (!key) return source.value
  const col = props.columns.find((c) => c.key === key)
  if (!col) return source.value
  const dir = sortDir.value === 'asc' ? 1 : -1
  return [...source.value].sort((r1, r2) => dir * compare(sortValueOf(col, r1), sortValueOf(col, r2)))
})

function ariaSort(col: Column): 'ascending' | 'descending' | 'none' {
  if (sortKey.value !== col.key) return 'none'
  return sortDir.value === 'asc' ? 'ascending' : 'descending'
}

// Keep the exposed filtered set in sync for parents that bind v-model:filtered (export matches the view,
// including the active sort order).
watch(sorted, (rows) => { filtered.value = rows }, { immediate: true })

const page = ref(1)
// Snap back to page 1 whenever the data, query, or sort changes.
watch([() => props.rows, query, sortKey, sortDir], () => {
  page.value = 1
})

const paginated = computed(() => props.pageSize > 0 && sorted.value.length > props.pageSize)
const totalPages = computed(() =>
  paginated.value ? Math.ceil(sorted.value.length / props.pageSize) : 1,
)
const pagedRows = computed(() => {
  if (!paginated.value) return sorted.value
  const start = (page.value - 1) * props.pageSize
  return sorted.value.slice(start, start + props.pageSize)
})
const rangeFrom = computed(() => (sorted.value.length === 0 ? 0 : (page.value - 1) * props.pageSize + 1))
const rangeTo = computed(() => Math.min(page.value * props.pageSize, sorted.value.length))

function go(delta: number) {
  page.value = Math.min(totalPages.value, Math.max(1, page.value + delta))
}

// One control to reset the search box, the column sort, and (via `clear`) the parent's filters.
const showClear = computed(() => !!query.value || !!sortKey.value || props.filtersActive)
function clearAll() {
  query.value = ''
  sortKey.value = null
  sortDir.value = 'asc'
  emit('clear')
}
</script>

<template>
  <div class="overflow-hidden rounded-card border border-border bg-surface shadow-card">
    <div v-if="searchable || $slots.filters || showClear" class="flex flex-col gap-2.5 border-b border-border p-2.5 sm:flex-row sm:items-center">
      <div v-if="searchable" class="relative w-full sm:max-w-xs">
        <Search :size="15" class="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" />
        <input
          v-model="query"
          type="search"
          :placeholder="searchPlaceholder ?? t('common.search')"
          class="field-input h-9 w-full pl-9 text-sm"
        />
      </div>
      <!-- Per-list filters + Clear, kept on a single row (auto-width controls, horizontal-scroll if tight). -->
      <div v-if="$slots.filters || showClear" class="flex flex-nowrap items-center gap-2 overflow-x-auto sm:ml-auto [&_input]:w-auto [&_select]:w-auto">
        <slot name="filters" />
        <button
          v-if="showClear"
          type="button"
          class="inline-flex shrink-0 items-center gap-1 rounded-button border border-border px-2.5 py-2 text-sm font-medium text-text-muted transition-colors hover:bg-surface-2 hover:text-text"
          @click="clearAll"
        >
          <X :size="14" /> {{ t('common.clear') }}
        </button>
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
              :class="[col.align === 'right' ? 'text-right' : 'text-left', col.hideOnMobile ? 'hidden sm:table-cell' : '']"
              :aria-sort="isSortable(col) ? ariaSort(col) : undefined"
            >
              <button
                v-if="isSortable(col) && col.label"
                type="button"
                class="group inline-flex items-center gap-1 uppercase tracking-wide transition-colors hover:text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent rounded"
                :class="[col.align === 'right' ? 'flex-row-reverse' : '', sortKey === col.key ? 'text-text' : '']"
                @click="toggleSort(col)"
              >
                <span>{{ col.label }}</span>
                <component
                  :is="sortKey === col.key ? (sortDir === 'asc' ? ArrowUp : ArrowDown) : ChevronsUpDown"
                  :size="13"
                  :class="sortKey === col.key ? 'text-accent' : 'text-text-muted/40 group-hover:text-text-muted'"
                />
              </button>
              <template v-else>{{ col.label }}</template>
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
              :class="[col.align === 'right' ? 'text-right' : 'text-left', col.numeric ? 'tnum' : '', col.hideOnMobile ? 'hidden sm:table-cell' : '']"
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
