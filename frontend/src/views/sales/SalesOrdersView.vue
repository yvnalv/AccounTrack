<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { Plus, Wallet } from 'lucide-vue-next'
import { salesApi } from '@/lib/sales'
import { masterData, nameMap } from '@/lib/masterData'
import { downloadExport } from '@/lib/api'
import { formatMoney } from '@/lib/format'
import type { SalesOrderSummary } from '@/types/sales'
import AppButton from '@/components/ui/AppButton.vue'
import DataTable from '@/components/ui/DataTable.vue'
import ExportMenu from '@/components/ui/ExportMenu.vue'
import type { Column } from '@/components/ui/types'
import StatusBadge from '@/components/ui/StatusBadge.vue'

const { t } = useI18n()
const router = useRouter()

const orders = ref<SalesOrderSummary[]>([])
const customers = ref(new Map<string, string>())
const loading = ref(true)

const columns = computed<Column[]>(() => [
  { key: 'number', label: t('sales.columns.number') },
  { key: 'customer', label: t('sales.columns.customer') },
  { key: 'orderDate', label: t('sales.columns.date') },
  { key: 'status', label: t('sales.columns.status') },
  { key: 'grandTotal', label: t('sales.columns.total'), align: 'right', numeric: true },
])

const rows = computed(() =>
  orders.value.map((o) => ({ ...o, customer: customers.value.get(o.customerId) ?? '—' })),
)

onMounted(async () => {
  try {
    const [list, custs] = await Promise.all([salesApi.list(), masterData.customers()])
    orders.value = list
    customers.value = nameMap(custs)
  } finally {
    loading.value = false
  }
})

function openOrder(row: Record<string, unknown>) {
  router.push({ name: 'salesOrderDetail', params: { id: String(row.id) } })
}
</script>

<template>
  <div class="space-y-4">
    <div class="flex justify-end gap-2">
      <ExportMenu :download="(f) => downloadExport('/sales-orders/export', 'sales-orders', f)" />
      <AppButton variant="secondary" @click="router.push({ name: 'salesReceivePayment' })">
        <Wallet :size="16" /> {{ t('sales.receivePayment') }}
      </AppButton>
      <AppButton @click="router.push({ name: 'salesOrderCreate' })">
        <Plus :size="16" /> {{ t('sales.new') }}
      </AppButton>
    </div>

    <DataTable
      :columns="columns"
      :rows="rows"
      :loading="loading"
      :empty-text="t('sales.empty')"
      clickable
      @row-click="openOrder"
    >
      <template #cell-status="{ value }">
        <StatusBadge :status="String(value)" :label="t(`sales.status.${value}`)" />
      </template>
      <template #cell-grandTotal="{ value }">
        {{ formatMoney(Number(value)) }}
      </template>
    </DataTable>
  </div>
</template>
