<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { salesApi } from '@/lib/sales'
import { formatMoney, formatMoneyShort } from '@/lib/format'
import type { SalesReturnListItem } from '@/types/sales'
import DataTable from '@/components/ui/DataTable.vue'
import InsightCards, { type Insight } from '@/components/ui/InsightCards.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import type { Column } from '@/components/ui/types'

const { t } = useI18n()
const router = useRouter()

const rows = ref<SalesReturnListItem[]>([])
const loading = ref(true)
const customerFilter = ref('')
const customerOptions = computed(() =>
  [...new Set(rows.value.map((r) => r.customerName).filter(Boolean))].sort() as string[],
)
const visibleRows = computed(() =>
  rows.value.filter((r) => !customerFilter.value || r.customerName === customerFilter.value),
)

function open(row: Record<string, unknown>) {
  router.push({ name: 'salesReturnDetail', params: { id: String(row.id) } })
}

const columns = computed<Column[]>(() => [
  { key: 'number', label: t('returns.columns.number') },
  { key: 'customerName', label: t('returns.columns.customer') },
  { key: 'returnDate', label: t('returns.columns.date'), hideOnMobile: true },
  { key: 'grandTotal', label: t('returns.columns.total'), align: 'right', numeric: true },
  { key: 'journalEntryId', label: t('returns.columns.status'), align: 'right' },
])

const insights = computed<Insight[]>(() => {
  const total = rows.value.reduce((s, r) => s + r.grandTotal, 0)
  return [
    { label: t('returns.insights.credits'), value: String(rows.value.length) },
    { label: t('returns.insights.value'), value: formatMoneyShort(total), tone: 'accent' },
  ]
})

onMounted(async () => {
  try {
    rows.value = await salesApi.allReturns()
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="space-y-4">
    <InsightCards :items="insights" />
    <DataTable searchable clickable :columns="columns" :rows="visibleRows" :loading="loading" :empty-text="t('returns.salesEmpty')" :filters-active="!!customerFilter" @row-click="open" @clear="customerFilter = ''">
      <template #filters>
        <select v-model="customerFilter" class="field-input h-9 text-sm">
          <option value="">{{ t('sales.allCustomers') }}</option>
          <option v-for="c in customerOptions" :key="c" :value="c">{{ c }}</option>
        </select>
      </template>
      <template #cell-grandTotal="{ value }">{{ formatMoney(Number(value)) }}</template>
      <template #cell-journalEntryId="{ value }">
        <StatusBadge v-if="value" tone="positive" :label="t('returns.posted')" />
      </template>
    </DataTable>
  </div>
</template>
