<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { purchasingApi } from '@/lib/purchasing'
import { formatMoney, formatMoneyShort } from '@/lib/format'
import type { SupplierPaymentListItem } from '@/types/purchasing'
import DataTable from '@/components/ui/DataTable.vue'
import InsightCards, { type Insight } from '@/components/ui/InsightCards.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import type { Column } from '@/components/ui/types'

const { t } = useI18n()
const router = useRouter()

const rows = ref<SupplierPaymentListItem[]>([])
const loading = ref(true)

function open(row: Record<string, unknown>) {
  router.push({ name: 'supplierPaymentDetail', params: { id: String(row.id) } })
}

const columns = computed<Column[]>(() => [
  { key: 'number', label: t('paymentList.columns.number') },
  { key: 'supplierName', label: t('paymentList.columns.supplier') },
  { key: 'paymentDate', label: t('paymentList.columns.date') },
  { key: 'totalAmount', label: t('paymentList.columns.amount'), align: 'right', numeric: true },
  { key: 'journalEntryId', label: t('paymentList.columns.status'), align: 'right' },
])

const insights = computed<Insight[]>(() => {
  const total = rows.value.reduce((s, r) => s + r.totalAmount, 0)
  return [
    { label: t('paymentList.insights.count'), value: String(rows.value.length) },
    { label: t('paymentList.insights.paid'), value: formatMoneyShort(total), tone: 'accent' },
  ]
})

onMounted(async () => {
  try {
    rows.value = await purchasingApi.allSupplierPayments()
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="space-y-4">
    <InsightCards :items="insights" />
    <DataTable searchable clickable :columns="columns" :rows="rows" :loading="loading" :empty-text="t('paymentList.purchaseEmpty')" @row-click="open">
      <template #cell-totalAmount="{ value }">{{ formatMoney(Number(value)) }}</template>
      <template #cell-journalEntryId="{ value }">
        <StatusBadge :tone="value ? 'positive' : 'neutral'" :label="value ? t('paymentList.posted') : t('paymentList.draft')" />
      </template>
    </DataTable>
  </div>
</template>
