<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { salesApi } from '@/lib/sales'
import { formatMoney, formatMoneyShort } from '@/lib/format'
import type { DeliveryListItem } from '@/types/sales'
import DataTable from '@/components/ui/DataTable.vue'
import InsightCards, { type Insight } from '@/components/ui/InsightCards.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import type { Column } from '@/components/ui/types'

const { t } = useI18n()
const router = useRouter()

const rows = ref<DeliveryListItem[]>([])
const loading = ref(true)

// A delivery belongs to its sales order — open the SO detail (its Deliveries card shows the DO).
function open(row: Record<string, unknown>) {
  router.push({ name: 'salesOrderDetail', params: { id: String(row.salesOrderId) } })
}

const columns = computed<Column[]>(() => [
  { key: 'number', label: t('deliveries.columns.number') },
  { key: 'customerName', label: t('deliveries.columns.customer') },
  { key: 'deliveryDate', label: t('deliveries.columns.date') },
  { key: 'totalCost', label: t('deliveries.columns.cogs'), align: 'right', numeric: true },
  { key: 'journalEntryId', label: t('deliveries.columns.status'), align: 'right' },
])

const insights = computed<Insight[]>(() => {
  const total = rows.value.reduce((s, r) => s + r.totalCost, 0)
  return [
    { label: t('deliveries.insights.count'), value: String(rows.value.length) },
    { label: t('deliveries.insights.cogs'), value: formatMoneyShort(total), tone: 'accent' },
  ]
})

onMounted(async () => {
  try {
    rows.value = await salesApi.allDeliveries()
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="space-y-4">
    <InsightCards :items="insights" />
    <p class="text-xs text-text-muted">{{ t('deliveries.hint') }}</p>
    <DataTable searchable clickable :columns="columns" :rows="rows" :loading="loading" :empty-text="t('deliveries.empty')" @row-click="open">
      <template #cell-totalCost="{ value }">{{ formatMoney(Number(value)) }}</template>
      <template #cell-journalEntryId="{ value }">
        <StatusBadge v-if="value" tone="positive" :label="t('deliveries.posted')" />
      </template>
    </DataTable>
  </div>
</template>
