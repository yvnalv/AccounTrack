<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { salesApi } from '@/lib/sales'
import { formatMoney, formatMoneyShort } from '@/lib/format'
import type { CustomerPaymentListItem } from '@/types/sales'
import DataTable from '@/components/ui/DataTable.vue'
import InsightCards, { type Insight } from '@/components/ui/InsightCards.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import type { Column } from '@/components/ui/types'

const { t } = useI18n()
const router = useRouter()

const rows = ref<CustomerPaymentListItem[]>([])
const loading = ref(true)
const customerFilter = ref('')
const customerOptions = computed(() =>
  [...new Set(rows.value.map((r) => r.customerName).filter(Boolean))].sort() as string[],
)
const visibleRows = computed(() =>
  rows.value.filter((r) => !customerFilter.value || r.customerName === customerFilter.value),
)

function open(row: Record<string, unknown>) {
  router.push({ name: 'customerPaymentDetail', params: { id: String(row.id) } })
}

const columns = computed<Column[]>(() => [
  { key: 'number', label: t('paymentList.columns.number') },
  { key: 'customerName', label: t('paymentList.columns.customer') },
  { key: 'paymentDate', label: t('paymentList.columns.date'), hideOnMobile: true },
  { key: 'totalAmount', label: t('paymentList.columns.amount'), align: 'right', numeric: true },
  { key: 'journalEntryId', label: t('paymentList.columns.status'), align: 'right' },
])

const insights = computed<Insight[]>(() => {
  const total = rows.value.reduce((s, r) => s + r.totalAmount, 0)
  return [
    { label: t('paymentList.insights.count'), value: String(rows.value.length) },
    { label: t('paymentList.insights.received'), value: formatMoneyShort(total), tone: 'accent' },
  ]
})

onMounted(async () => {
  try {
    rows.value = await salesApi.allCustomerPayments()
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="space-y-4">
    <InsightCards :items="insights" />
    <DataTable searchable clickable :columns="columns" :rows="visibleRows" :loading="loading" :empty-text="t('paymentList.salesEmpty')" :filters-active="!!customerFilter" @row-click="open" @clear="customerFilter = ''">
      <template #filters>
        <select v-model="customerFilter" class="field-input h-9 text-sm">
          <option value="">{{ t('sales.allCustomers') }}</option>
          <option v-for="c in customerOptions" :key="c" :value="c">{{ c }}</option>
        </select>
      </template>
      <template #cell-totalAmount="{ value }">{{ formatMoney(Number(value)) }}</template>
      <template #cell-journalEntryId="{ value }">
        <StatusBadge :tone="value ? 'positive' : 'neutral'" :label="value ? t('paymentList.posted') : t('paymentList.draft')" />
      </template>
    </DataTable>
  </div>
</template>
