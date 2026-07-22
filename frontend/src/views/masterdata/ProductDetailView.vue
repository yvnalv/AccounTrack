<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { ArrowLeft } from 'lucide-vue-next'
import { masterData, nameMap } from '@/lib/masterData'
import { inventoryApi } from '@/lib/inventory'
import { formatMoney, formatMoneyShort, formatNumber } from '@/lib/format'
import type { Product } from '@/types/masterdata'
import type { StockCardEntry, StockOnHand } from '@/types/inventory'
import AppCard from '@/components/ui/AppCard.vue'
import InsightCards, { type Insight } from '@/components/ui/InsightCards.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()

const id = computed(() => String(route.params.id))
const loading = ref(true)
const product = ref<Product | null>(null)
const categoryName = ref('—')
const uomCode = ref('')
const warehouseNames = ref(new Map<string, string>())
const onHand = ref<StockOnHand[]>([])
const movements = ref<StockCardEntry[]>([])

const totalQty = computed(() => onHand.value.reduce((s, r) => s + r.onHandQty, 0))
const totalValue = computed(() => onHand.value.reduce((s, r) => s + r.value, 0))
const avgCost = computed(() => (totalQty.value > 0 ? totalValue.value / totalQty.value : 0))

const insights = computed<Insight[]>(() => [
  { label: t('masterData.products.currentStock'), value: `${formatNumber(totalQty.value)} ${uomCode.value}`, tone: totalQty.value <= 0 ? 'negative' : 'neutral' },
  { label: t('masterData.products.stockValue'), value: formatMoneyShort(totalValue.value), tone: 'accent' },
  { label: t('inventory.card.runningAvg'), value: formatMoney(avgCost.value) },
  { label: t('masterData.products.salePrice'), value: product.value?.salePrice != null ? formatMoney(product.value.salePrice) : '—' },
])

// Signed quantity for display: outbound movements reduce stock.
const OUTBOUND = new Set(['Issue', 'AdjustmentOut', 'TransferOut', 'ProductionConsume'])
const signedQty = (m: StockCardEntry) => (OUTBOUND.has(m.type) ? -m.quantity : m.quantity)

async function load() {
  loading.value = true
  try {
    const [products, cats, uoms, warehouses, allOnHand] = await Promise.all([
      masterData.products(),
      masterData.productCategories(),
      masterData.unitsOfMeasure(),
      masterData.warehouses(),
      inventoryApi.onHand().catch(() => [] as StockOnHand[]),
    ])
    product.value = products.find((p) => p.id === id.value) ?? null
    categoryName.value = (product.value?.categoryId && nameMap(cats).get(product.value.categoryId)) || '—'
    uomCode.value = (product.value && uoms.find((u) => u.id === product.value?.baseUomId)?.code) || ''
    warehouseNames.value = new Map(warehouses.map((w) => [w.id, w.name]))
    onHand.value = allOnHand.filter((s) => s.productId === id.value)

    movements.value = await inventoryApi.stockCard(id.value).catch(() => [])
  } finally {
    loading.value = false
  }
}
onMounted(load)
</script>

<template>
  <div class="space-y-5">
    <button class="inline-flex items-center gap-1.5 text-sm text-text-muted hover:text-text" @click="router.push({ name: 'masterDataProducts' })">
      <ArrowLeft :size="16" /> {{ t('nav.products') }}
    </button>

    <p v-if="loading" class="text-sm text-text-muted">{{ t('common.loading') }}</p>
    <p v-else-if="!product" class="text-sm text-text-muted">{{ t('party.notFound') }}</p>

    <template v-else>
      <div class="flex flex-wrap items-start justify-between gap-4">
        <div>
          <div class="flex items-center gap-3">
            <h2 class="text-xl font-semibold text-text">{{ product.name }}</h2>
            <StatusBadge :label="product.isActive ? t('masterData.active') : t('masterData.inactive')" :tone="product.isActive ? 'positive' : 'neutral'" />
          </div>
          <p class="mt-1 text-sm text-text-muted">
            {{ product.code }} · {{ categoryName }} · {{ uomCode }} ·
            {{ product.costingMethod === 'Fifo' ? t('masterData.products.costing.fifo') : t('masterData.products.costing.movingAverage') }}
          </p>
        </div>
      </div>

      <InsightCards :items="insights" />

      <!-- On hand split by warehouse -->
      <AppCard :title="t('productDetail.byWarehouse')" :padded="false">
        <table v-if="onHand.length" class="w-full text-sm">
          <thead>
            <tr class="border-b border-border text-xs uppercase tracking-wide text-text-muted">
              <th class="px-4 py-2.5 text-left font-semibold">{{ t('productDetail.warehouse') }}</th>
              <th class="px-3 py-2.5 text-right font-semibold">{{ t('warehouseDetail.onHand') }}</th>
              <th class="px-3 py-2.5 text-right font-semibold">{{ t('warehouseDetail.avgCost') }}</th>
              <th class="px-4 py-2.5 text-right font-semibold">{{ t('warehouseDetail.value') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="s in onHand" :key="s.warehouseId" class="border-b border-border last:border-0">
              <td class="px-4 py-2.5 text-text">{{ warehouseNames.get(s.warehouseId) ?? '—' }}</td>
              <td class="px-3 py-2.5 text-right tnum" :class="s.onHandQty <= 0 ? 'text-negative' : 'text-text'">{{ formatNumber(s.onHandQty) }}</td>
              <td class="px-3 py-2.5 text-right text-text-muted tnum">{{ formatMoney(s.avgUnitCost) }}</td>
              <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(s.value) }}</td>
            </tr>
          </tbody>
        </table>
        <p v-else class="px-4 py-6 text-center text-sm text-text-muted">{{ t('warehouseDetail.empty') }}</p>
      </AppCard>

      <!-- Movement history (purchases in, sales out, adjustments, transfers) -->
      <AppCard :title="t('productDetail.history')" :padded="false">
        <div class="overflow-x-auto">
          <table v-if="movements.length" class="w-full text-sm">
            <thead>
              <tr class="border-b border-border text-xs uppercase tracking-wide text-text-muted">
                <th class="px-4 py-2.5 text-left font-semibold">{{ t('inventory.card.date') }}</th>
                <th class="px-3 py-2.5 text-left font-semibold">{{ t('inventory.card.type') }}</th>
                <th class="px-3 py-2.5 text-left font-semibold">{{ t('inventory.card.source') }}</th>
                <th class="px-3 py-2.5 text-right font-semibold">{{ t('inventory.card.qty') }}</th>
                <th class="px-3 py-2.5 text-right font-semibold">{{ t('inventory.card.unitCost') }}</th>
                <th class="px-4 py-2.5 text-right font-semibold">{{ t('inventory.card.runningQty') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="m in movements" :key="m.transactionId" class="border-b border-border last:border-0">
                <td class="px-4 py-2.5 text-text-muted">{{ m.date }}</td>
                <td class="px-3 py-2.5 text-text">{{ t(`inventory.types.${m.type}`) }}</td>
                <td class="px-3 py-2.5 text-text-muted">{{ t(`inventory.sources.${m.source}`) }}</td>
                <td class="px-3 py-2.5 text-right tnum" :class="signedQty(m) < 0 ? 'text-negative' : 'text-positive'">
                  {{ signedQty(m) > 0 ? '+' : '' }}{{ formatNumber(signedQty(m)) }}
                </td>
                <td class="px-3 py-2.5 text-right text-text-muted tnum">{{ formatMoney(m.unitCost) }}</td>
                <td class="px-4 py-2.5 text-right text-text tnum">{{ formatNumber(m.runningQtyAfter) }}</td>
              </tr>
            </tbody>
          </table>
          <p v-else class="px-4 py-6 text-center text-sm text-text-muted">{{ t('inventory.card.empty') }}</p>
        </div>
      </AppCard>
    </template>
  </div>
</template>
