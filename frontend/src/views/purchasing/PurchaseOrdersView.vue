<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { Plus } from 'lucide-vue-next'
import { purchasingApi } from '@/lib/purchasing'
import { masterData, nameMap } from '@/lib/masterData'
import { formatMoney } from '@/lib/format'
import type { PurchaseOrderSummary } from '@/types/purchasing'
import AppButton from '@/components/ui/AppButton.vue'
import DataTable from '@/components/ui/DataTable.vue'
import type { Column } from '@/components/ui/types'
import StatusBadge from '@/components/ui/StatusBadge.vue'

const { t } = useI18n()
const router = useRouter()

const orders = ref<PurchaseOrderSummary[]>([])
const suppliers = ref(new Map<string, string>())
const loading = ref(true)

const columns = computed<Column[]>(() => [
  { key: 'number', label: t('purchasing.columns.number') },
  { key: 'supplier', label: t('purchasing.columns.supplier') },
  { key: 'orderDate', label: t('purchasing.columns.date') },
  { key: 'status', label: t('purchasing.columns.status') },
  { key: 'grandTotal', label: t('purchasing.columns.total'), align: 'right', numeric: true },
])

const rows = computed(() =>
  orders.value.map((o) => ({ ...o, supplier: suppliers.value.get(o.supplierId) ?? '—' })),
)

onMounted(async () => {
  try {
    const [list, sups] = await Promise.all([purchasingApi.list(), masterData.suppliers()])
    orders.value = list
    suppliers.value = nameMap(sups)
  } finally {
    loading.value = false
  }
})

function open(row: Record<string, unknown>) {
  router.push({ name: 'purchaseOrderDetail', params: { id: String(row.id) } })
}
</script>

<template>
  <div class="space-y-4">
    <div class="flex justify-end">
      <AppButton @click="router.push({ name: 'purchaseOrderCreate' })">
        <Plus :size="16" /> {{ t('purchasing.new') }}
      </AppButton>
    </div>

    <DataTable
      :columns="columns"
      :rows="rows"
      :loading="loading"
      :empty-text="t('purchasing.empty')"
      clickable
      @row-click="open"
    >
      <template #cell-status="{ value }">
        <StatusBadge :status="String(value)" :label="t(`purchasing.status.${value}`)" />
      </template>
      <template #cell-grandTotal="{ value }">{{ formatMoney(Number(value)) }}</template>
    </DataTable>
  </div>
</template>
