<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { ArrowLeft } from 'lucide-vue-next'
import { masterData, nameMap } from '@/lib/masterData'
import { inventoryApi } from '@/lib/inventory'
import { formatMoney, formatMoneyShort, formatNumber } from '@/lib/format'
import type { Warehouse } from '@/types/masterdata'
import AppCard from '@/components/ui/AppCard.vue'
import InsightCards, { type Insight } from '@/components/ui/InsightCards.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'

interface StockRow {
  productId: string
  productName: string
  onHandQty: number
  avgUnitCost: number
  value: number
}

const { t } = useI18n()
const route = useRoute()
const router = useRouter()

const id = computed(() => String(route.params.id))
const loading = ref(true)
const warehouse = ref<Warehouse | null>(null)
const stock = ref<StockRow[]>([])

const stockValue = computed(() => stock.value.reduce((s, r) => s + r.value, 0))
const totalUnits = computed(() => stock.value.reduce((s, r) => s + r.onHandQty, 0))

const insights = computed<Insight[]>(() => [
  { label: t('masterData.warehouses.stockValue'), value: formatMoneyShort(stockValue.value), tone: 'accent' },
  { label: t('masterData.warehouses.skus'), value: String(stock.value.length) },
  { label: t('warehouseDetail.units'), value: formatNumber(totalUnits.value) },
  { label: t('masterData.status'), value: warehouse.value?.isActive ? t('masterData.active') : t('masterData.inactive') },
])

async function load() {
  loading.value = true
  try {
    const [warehouses, products, onHand] = await Promise.all([
      masterData.warehouses(),
      masterData.products(),
      inventoryApi.onHand().catch(() => []),
    ])
    warehouse.value = warehouses.find((w) => w.id === id.value) ?? null
    const names = nameMap(products)
    stock.value = onHand
      .filter((s) => s.warehouseId === id.value && (s.onHandQty !== 0 || s.value !== 0))
      .map((s) => ({
        productId: s.productId,
        productName: names.get(s.productId) ?? '—',
        onHandQty: s.onHandQty,
        avgUnitCost: s.avgUnitCost,
        value: s.value,
      }))
      .sort((a, b) => b.value - a.value)
  } finally {
    loading.value = false
  }
}
onMounted(load)
</script>

<template>
  <div class="space-y-5">
    <button class="inline-flex items-center gap-1.5 text-sm text-text-muted hover:text-text" @click="router.push({ name: 'masterDataWarehouses' })">
      <ArrowLeft :size="16" /> {{ t('warehouseDetail.back') }}
    </button>

    <p v-if="loading" class="text-sm text-text-muted">{{ t('common.loading') }}</p>
    <p v-else-if="!warehouse" class="text-sm text-text-muted">{{ t('party.notFound') }}</p>

    <template v-else>
      <div class="flex flex-wrap items-start justify-between gap-4">
        <div>
          <div class="flex items-center gap-3">
            <h2 class="text-xl font-semibold text-text">{{ warehouse.name }}</h2>
            <StatusBadge :label="warehouse.isActive ? t('masterData.active') : t('masterData.inactive')" :tone="warehouse.isActive ? 'positive' : 'neutral'" />
          </div>
          <p class="mt-1 text-sm text-text-muted">
            {{ warehouse.code }}<template v-if="warehouse.address"> · {{ warehouse.address }}</template>
          </p>
        </div>
      </div>

      <InsightCards :items="insights" />

      <AppCard :title="t('warehouseDetail.contents')" :padded="false">
        <table v-if="stock.length" class="w-full text-sm">
          <thead>
            <tr class="border-b border-border text-xs uppercase tracking-wide text-text-muted">
              <th class="px-4 py-2.5 text-left font-semibold">{{ t('warehouseDetail.product') }}</th>
              <th class="px-3 py-2.5 text-right font-semibold">{{ t('warehouseDetail.onHand') }}</th>
              <th class="px-3 py-2.5 text-right font-semibold">{{ t('warehouseDetail.avgCost') }}</th>
              <th class="px-4 py-2.5 text-right font-semibold">{{ t('warehouseDetail.value') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="r in stock" :key="r.productId" class="border-b border-border last:border-0">
              <td class="px-4 py-2.5 text-text">{{ r.productName }}</td>
              <td class="px-3 py-2.5 text-right tnum" :class="r.onHandQty <= 0 ? 'text-negative' : 'text-text'">{{ formatNumber(r.onHandQty) }}</td>
              <td class="px-3 py-2.5 text-right text-text-muted tnum">{{ formatMoney(r.avgUnitCost) }}</td>
              <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(r.value) }}</td>
            </tr>
          </tbody>
          <tfoot>
            <tr class="border-t border-border font-semibold">
              <td class="px-4 py-2.5 text-text" colspan="3">{{ t('warehouseDetail.total') }}</td>
              <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(stockValue) }}</td>
            </tr>
          </tfoot>
        </table>
        <p v-else class="px-4 py-6 text-center text-sm text-text-muted">{{ t('warehouseDetail.empty') }}</p>
      </AppCard>
    </template>
  </div>
</template>
