<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { purchasingApi } from '@/lib/purchasing'
import { formatMoney, formatMoneyShort } from '@/lib/format'
import type { PurchaseInvoiceListItem } from '@/types/purchasing'
import DataTable from '@/components/ui/DataTable.vue'
import InsightCards, { type Insight } from '@/components/ui/InsightCards.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import type { Column } from '@/components/ui/types'

const { t } = useI18n()
const router = useRouter()

const rows = ref<PurchaseInvoiceListItem[]>([])
const loading = ref(true)
const supplierFilter = ref('')
const supplierOptions = computed(() =>
  [...new Set(rows.value.map((r) => r.supplierName).filter(Boolean))].sort() as string[],
)
const visibleRows = computed(() =>
  rows.value.filter((r) => !supplierFilter.value || r.supplierName === supplierFilter.value),
)

function open(row: Record<string, unknown>) {
  router.push({ name: 'purchaseInvoiceDetail', params: { id: String(row.id) } })
}

const columns = computed<Column[]>(() => [
  { key: 'number', label: t('invoiceList.columns.number') },
  { key: 'supplierName', label: t('invoiceList.columns.supplier') },
  { key: 'invoiceDate', label: t('invoiceList.columns.date'), hideOnMobile: true },
  { key: 'dueDate', label: t('invoiceList.columns.due'), hideOnMobile: true },
  { key: 'grandTotal', label: t('invoiceList.columns.total'), align: 'right', numeric: true },
  { key: 'journalEntryId', label: t('invoiceList.columns.status'), align: 'right' },
])

const insights = computed<Insight[]>(() => {
  const total = rows.value.reduce((s, r) => s + r.grandTotal, 0)
  return [
    { label: t('invoiceList.insights.count'), value: String(rows.value.length) },
    { label: t('invoiceList.insights.total'), value: formatMoneyShort(total), tone: 'accent' },
  ]
})

onMounted(async () => {
  try {
    rows.value = await purchasingApi.allInvoices()
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="space-y-4">
    <InsightCards :items="insights" />
    <DataTable searchable clickable :columns="columns" :rows="visibleRows" :loading="loading" :empty-text="t('invoiceList.purchaseEmpty')" :filters-active="!!supplierFilter" @row-click="open" @clear="supplierFilter = ''">
      <template #filters>
        <select v-model="supplierFilter" class="field-input h-9 text-sm">
          <option value="">{{ t('purchasing.allSuppliers') }}</option>
          <option v-for="s in supplierOptions" :key="s" :value="s">{{ s }}</option>
        </select>
      </template>
      <template #cell-grandTotal="{ value }">{{ formatMoney(Number(value)) }}</template>
      <template #cell-journalEntryId="{ value }">
        <StatusBadge :tone="value ? 'positive' : 'neutral'" :label="value ? t('invoiceList.posted') : t('invoiceList.draft')" />
      </template>
    </DataTable>
  </div>
</template>
