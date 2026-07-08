<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { ArrowLeft, ExternalLink } from 'lucide-vue-next'
import { purchasingApi } from '@/lib/purchasing'
import { masterData, nameMap } from '@/lib/masterData'
import { formatMoney, formatNumber } from '@/lib/format'
import type { GoodsReceipt } from '@/types/purchasing'
import AppCard from '@/components/ui/AppCard.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()

const doc = ref<GoodsReceipt | null>(null)
const products = ref(new Map<string, string>())
const suppliers = ref(new Map<string, string>())
const loading = ref(true)

const id = computed(() => String(route.params.id))
const currency = computed(() => doc.value?.currency ?? 'IDR')

async function load() {
  loading.value = true
  try {
    const [r, prods, sups] = await Promise.all([
      purchasingApi.getReceipt(id.value),
      masterData.products(),
      masterData.suppliers(),
    ])
    doc.value = r
    products.value = nameMap(prods)
    suppliers.value = nameMap(sups)
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="space-y-5">
    <button
      class="inline-flex items-center gap-1.5 text-sm text-text-muted hover:text-text"
      @click="doc ? router.push({ name: 'purchaseOrderDetail', params: { id: doc.purchaseOrderId } }) : router.back()"
    >
      <ArrowLeft :size="16" /> {{ t('docDetail.backToPurchaseOrder') }}
    </button>

    <p v-if="loading" class="text-sm text-text-muted">{{ t('docDetail.loading') }}</p>
    <p v-else-if="!doc" class="text-sm text-text-muted">{{ t('docDetail.notFound') }}</p>

    <template v-else>
      <div class="flex flex-wrap items-start justify-between gap-4">
        <div>
          <div class="flex items-center gap-3">
            <h2 class="text-xl font-semibold text-text">{{ doc.number }}</h2>
            <StatusBadge tone="info" :label="t('docDetail.receiptTitle')" />
            <StatusBadge
              :tone="doc.journalEntryId ? 'positive' : 'neutral'"
              :label="doc.journalEntryId ? t('docDetail.posted') : t('docDetail.notPosted')"
            />
          </div>
          <p class="mt-1 text-sm text-text-muted">
            {{ suppliers.get(doc.supplierId) ?? '—' }} · {{ doc.receiptDate }}
          </p>
        </div>
        <button
          class="inline-flex items-center gap-1.5 rounded-button border border-border px-3 py-2 text-sm font-medium text-text-muted transition-colors hover:bg-surface-2 hover:text-text"
          @click="router.push({ name: 'purchaseOrderDetail', params: { id: doc.purchaseOrderId } })"
        >
          <ExternalLink :size="16" /> {{ t('docDetail.viewPurchaseOrder') }}
        </button>
      </div>

      <AppCard :title="t('docDetail.lines')" :padded="false">
        <div class="overflow-x-auto">
          <table class="w-full text-sm">
            <thead>
              <tr class="border-b border-border text-xs uppercase tracking-wide text-text-muted">
                <th class="px-4 py-2.5 text-left font-semibold">{{ t('docDetail.product') }}</th>
                <th class="px-3 py-2.5 text-right font-semibold">{{ t('docDetail.qty') }}</th>
                <th class="px-3 py-2.5 text-right font-semibold">{{ t('docDetail.unitCost') }}</th>
                <th class="px-4 py-2.5 text-right font-semibold">{{ t('docDetail.lineCost') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(line, i) in doc.lines" :key="i" class="border-b border-border last:border-0">
                <td class="px-4 py-3 text-text">{{ products.get(line.productId) ?? '—' }}</td>
                <td class="px-3 py-3 text-right text-text tnum">{{ formatNumber(line.quantity) }}</td>
                <td class="px-3 py-3 text-right text-text-muted tnum">{{ formatMoney(line.unitCost, currency) }}</td>
                <td class="px-4 py-3 text-right text-text tnum">{{ formatMoney(line.lineCost, currency) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <div class="flex justify-end border-t border-border p-4">
          <dl class="w-full max-w-xs space-y-1.5 text-sm">
            <div class="flex justify-between font-semibold">
              <dt class="text-text">{{ t('docDetail.totalCost') }}</dt>
              <dd class="tnum text-text">{{ formatMoney(doc.totalCost, currency) }}</dd>
            </div>
          </dl>
        </div>
        <p class="border-t border-border px-4 py-2 text-xs text-text-muted">{{ t('docDetail.receiptHint') }}</p>
      </AppCard>

      <AppCard v-if="doc.notes" :title="t('docDetail.notes')">
        <p class="text-sm text-text">{{ doc.notes }}</p>
      </AppCard>
    </template>
  </div>
</template>
