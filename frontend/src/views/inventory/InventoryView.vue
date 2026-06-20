<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { inventoryApi } from '@/lib/inventory'
import { masterData, nameMap } from '@/lib/masterData'
import { downloadExport } from '@/lib/api'
import { formatMoney, formatNumber } from '@/lib/format'
import type { StockOnHand } from '@/types/inventory'
import DataTable from '@/components/ui/DataTable.vue'
import ExportMenu from '@/components/ui/ExportMenu.vue'
import type { Column } from '@/components/ui/types'

const { t } = useI18n()
const router = useRouter()

const stock = ref<StockOnHand[]>([])
const products = ref(new Map<string, string>())
const warehouses = ref(new Map<string, string>())
const loading = ref(true)

const columns = computed<Column[]>(() => [
  { key: 'product', label: t('inventory.columns.product') },
  { key: 'warehouse', label: t('inventory.columns.warehouse') },
  { key: 'onHandQty', label: t('inventory.columns.onHand'), align: 'right', numeric: true },
  { key: 'avgUnitCost', label: t('inventory.columns.avgCost'), align: 'right', numeric: true },
  { key: 'value', label: t('inventory.columns.value'), align: 'right', numeric: true },
])

const rows = computed(() =>
  stock.value.map((s) => ({
    ...s,
    product: products.value.get(s.productId) ?? '—',
    warehouse: warehouses.value.get(s.warehouseId) ?? '—',
  })),
)

onMounted(async () => {
  try {
    const [oh, prods, whs] = await Promise.all([
      inventoryApi.onHand(),
      masterData.products(),
      masterData.warehouses(),
    ])
    stock.value = oh
    products.value = nameMap(prods)
    warehouses.value = nameMap(whs)
  } finally {
    loading.value = false
  }
})

function openCard(row: Record<string, unknown>) {
  router.push({ name: 'inventoryStockCard', query: { productId: String(row.productId), warehouseId: String(row.warehouseId) } })
}
</script>

<template>
  <div class="space-y-4">
    <div class="flex justify-end">
      <ExportMenu :download="(f) => downloadExport('/stock/on-hand/export', 'stock-on-hand', f)" />
    </div>
    <DataTable
      :columns="columns"
      :rows="rows"
      :loading="loading"
      :empty-text="t('inventory.empty')"
      clickable
      @row-click="openCard"
    >
      <template #cell-onHandQty="{ value }">{{ formatNumber(Number(value), 2) }}</template>
      <template #cell-avgUnitCost="{ value }">{{ formatMoney(Number(value)) }}</template>
      <template #cell-value="{ value }">{{ formatMoney(Number(value)) }}</template>
    </DataTable>
  </div>
</template>
