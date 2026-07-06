<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { purchasingApi } from '@/lib/purchasing'
import { formatMoney, formatMoneyShort } from '@/lib/format'
import type { PurchaseReturnListItem } from '@/types/purchasing'
import DataTable from '@/components/ui/DataTable.vue'
import InsightCards, { type Insight } from '@/components/ui/InsightCards.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import type { Column } from '@/components/ui/types'

const { t } = useI18n()
const router = useRouter()

const rows = ref<PurchaseReturnListItem[]>([])
const loading = ref(true)

function open(row: Record<string, unknown>) {
  router.push({ name: 'purchaseReturnDetail', params: { id: String(row.id) } })
}

const columns = computed<Column[]>(() => [
  { key: 'number', label: t('returns.columns.number') },
  { key: 'supplierName', label: t('returns.columns.supplier') },
  { key: 'returnDate', label: t('returns.columns.date') },
  { key: 'grandTotal', label: t('returns.columns.total'), align: 'right', numeric: true },
  { key: 'journalEntryId', label: t('returns.columns.status'), align: 'right' },
])

const insights = computed<Insight[]>(() => {
  const total = rows.value.reduce((s, r) => s + r.grandTotal, 0)
  return [
    { label: t('returns.insights.debits'), value: String(rows.value.length) },
    { label: t('returns.insights.value'), value: formatMoneyShort(total), tone: 'accent' },
  ]
})

onMounted(async () => {
  try {
    rows.value = await purchasingApi.allReturns()
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="space-y-4">
    <InsightCards :items="insights" />
    <DataTable searchable clickable :columns="columns" :rows="rows" :loading="loading" :empty-text="t('returns.purchaseEmpty')" @row-click="open">
      <template #cell-grandTotal="{ value }">{{ formatMoney(Number(value)) }}</template>
      <template #cell-journalEntryId="{ value }">
        <StatusBadge v-if="value" tone="positive" :label="t('returns.posted')" />
      </template>
    </DataTable>
  </div>
</template>
