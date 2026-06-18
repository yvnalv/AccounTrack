<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { ArrowLeft } from 'lucide-vue-next'
import { inventoryApi, isInbound } from '@/lib/inventory'
import { masterData, nameMap } from '@/lib/masterData'
import { formatMoney, formatNumber } from '@/lib/format'
import type { StockCardEntry } from '@/types/inventory'
import AppCard from '@/components/ui/AppCard.vue'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()

const entries = ref<StockCardEntry[]>([])
const productName = ref('')
const loading = ref(true)

const productId = computed(() => String(route.query.productId ?? ''))
const warehouseId = computed(() => (route.query.warehouseId ? String(route.query.warehouseId) : undefined))

function typeLabel(type: string) {
  const key = `inventory.types.${type}`
  const label = t(key)
  return label === key ? type : label
}

onMounted(async () => {
  try {
    const [card, prods] = await Promise.all([
      inventoryApi.stockCard(productId.value, warehouseId.value),
      masterData.products(),
    ])
    entries.value = card
    productName.value = nameMap(prods).get(productId.value) ?? '—'
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="space-y-4">
    <button
      class="inline-flex items-center gap-1.5 text-sm text-text-muted hover:text-text"
      @click="router.push({ name: 'inventory' })"
    >
      <ArrowLeft :size="16" /> {{ t('inventory.card.back') }}
    </button>

    <h2 class="text-lg font-semibold text-text">{{ t('inventory.card.heading', { product: productName }) }}</h2>

    <AppCard :padded="false">
      <table class="w-full text-sm">
        <thead>
          <tr class="border-b border-border text-xs uppercase tracking-wide text-text-muted">
            <th class="px-4 py-2.5 text-left font-semibold">{{ t('inventory.card.date') }}</th>
            <th class="px-4 py-2.5 text-left font-semibold">{{ t('inventory.card.type') }}</th>
            <th class="px-4 py-2.5 text-left font-semibold">{{ t('inventory.card.source') }}</th>
            <th class="px-4 py-2.5 text-right font-semibold">{{ t('inventory.card.qty') }}</th>
            <th class="px-4 py-2.5 text-right font-semibold">{{ t('inventory.card.unitCost') }}</th>
            <th class="px-4 py-2.5 text-right font-semibold">{{ t('inventory.card.runningQty') }}</th>
            <th class="px-4 py-2.5 text-right font-semibold">{{ t('inventory.card.runningAvg') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="loading"><td colspan="7" class="px-4 py-8 text-center text-text-muted">{{ t('common.loading') }}</td></tr>
          <tr v-else-if="entries.length === 0"><td colspan="7" class="px-4 py-10 text-center text-text-muted">{{ t('inventory.card.empty') }}</td></tr>
          <tr v-for="e in entries" v-else :key="e.transactionId" class="border-b border-border last:border-0">
            <td class="px-4 py-2.5 text-text-muted">{{ e.date }}</td>
            <td class="px-4 py-2.5 text-text">{{ typeLabel(e.type) }}</td>
            <td class="px-4 py-2.5 text-text-muted">{{ e.source }}</td>
            <td class="px-4 py-2.5 text-right tnum" :class="isInbound(e.type) ? 'text-positive' : 'text-negative'">
              {{ isInbound(e.type) ? '+' : '−' }}{{ formatNumber(e.quantity, 2) }}
            </td>
            <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(e.unitCost) }}</td>
            <td class="px-4 py-2.5 text-right text-text tnum">{{ formatNumber(e.runningQtyAfter, 2) }}</td>
            <td class="px-4 py-2.5 text-right text-text-muted tnum">{{ formatMoney(e.runningAvgCostAfter) }}</td>
          </tr>
        </tbody>
      </table>
    </AppCard>
  </div>
</template>
