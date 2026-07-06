<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { ArrowLeft, FileText, ExternalLink } from 'lucide-vue-next'
import { purchasingApi } from '@/lib/purchasing'
import { masterData, nameMap } from '@/lib/masterData'
import { downloadFile } from '@/lib/api'
import { formatMoney, formatNumber, formatPercent } from '@/lib/format'
import type { PurchaseReturn } from '@/types/purchasing'
import AppCard from '@/components/ui/AppCard.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()

const doc = ref<PurchaseReturn | null>(null)
const products = ref(new Map<string, string>())
const suppliers = ref(new Map<string, string>())
const loading = ref(true)

const id = computed(() => String(route.params.id))
const currency = computed(() => doc.value?.currency ?? 'IDR')

async function load() {
  loading.value = true
  try {
    const [r, prods, supps] = await Promise.all([
      purchasingApi.getReturn(id.value),
      masterData.products(),
      masterData.suppliers(),
    ])
    doc.value = r
    products.value = nameMap(prods)
    suppliers.value = nameMap(supps)
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
      @click="router.push({ name: 'purchaseReturns' })"
    >
      <ArrowLeft :size="16" /> {{ t('returns.detail.backToPurchases') }}
    </button>

    <p v-if="loading" class="text-sm text-text-muted">{{ t('common.loading') }}</p>

    <template v-else-if="doc">
      <!-- Header -->
      <div class="flex flex-wrap items-start justify-between gap-4">
        <div>
          <div class="flex items-center gap-3">
            <h2 class="text-xl font-semibold text-text">{{ doc.number }}</h2>
            <StatusBadge tone="info" :label="t('returns.detail.debitNote')" />
            <StatusBadge v-if="doc.journalEntryId" tone="positive" :label="t('returns.posted')" />
          </div>
          <p class="mt-1 text-sm text-text-muted">
            {{ suppliers.get(doc.supplierId) ?? '—' }} · {{ doc.returnDate }}
          </p>
        </div>
        <div class="flex flex-wrap items-center gap-2">
          <button
            class="inline-flex items-center gap-1.5 rounded-button border border-border px-3 py-2 text-sm font-medium text-text-muted transition-colors hover:bg-surface-2 hover:text-text"
            @click="downloadFile(`/purchase-invoices/${doc.purchaseInvoiceId}/pdf`, `purchase-invoice.pdf`)"
          >
            <FileText :size="16" /> {{ t('returns.detail.invoicePdf') }}
          </button>
          <button
            class="inline-flex items-center gap-1.5 rounded-button border border-border px-3 py-2 text-sm font-medium text-text-muted transition-colors hover:bg-surface-2 hover:text-text"
            @click="router.push({ name: 'purchaseOrderDetail', params: { id: doc.purchaseOrderId } })"
          >
            <ExternalLink :size="16" /> {{ t('returns.detail.viewPo') }}
          </button>
        </div>
      </div>

      <!-- Lines -->
      <AppCard :title="t('returns.detail.lines')" :padded="false">
        <div class="overflow-x-auto">
          <table class="w-full text-sm">
            <thead>
              <tr class="border-b border-border text-xs uppercase tracking-wide text-text-muted">
                <th class="px-4 py-2.5 text-left font-semibold">{{ t('returns.detail.product') }}</th>
                <th class="px-3 py-2.5 text-right font-semibold">{{ t('returns.detail.qty') }}</th>
                <th class="px-3 py-2.5 text-right font-semibold">{{ t('returns.detail.unitPrice') }}</th>
                <th class="px-3 py-2.5 text-right font-semibold">{{ t('returns.detail.taxPct') }}</th>
                <th class="px-3 py-2.5 text-right font-semibold">{{ t('returns.detail.cost') }}</th>
                <th class="px-4 py-2.5 text-right font-semibold">{{ t('returns.detail.lineTotal') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(line, i) in doc.lines" :key="i" class="border-b border-border last:border-0">
                <td class="px-4 py-3 text-text">{{ products.get(line.productId) ?? '—' }}</td>
                <td class="px-3 py-3 text-right text-text tnum">{{ formatNumber(line.quantity) }}</td>
                <td class="px-3 py-3 text-right text-text tnum">{{ formatMoney(line.unitPrice, currency) }}</td>
                <td class="px-3 py-3 text-right text-text-muted tnum">{{ formatPercent(line.taxRate * 100) }}</td>
                <td class="px-3 py-3 text-right text-text-muted tnum">{{ formatMoney(line.lineCost, currency) }}</td>
                <td class="px-4 py-3 text-right text-text tnum">{{ formatMoney(line.lineTotal, currency) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <div class="flex justify-end border-t border-border p-4">
          <dl class="w-full max-w-xs space-y-1.5 text-sm">
            <div class="flex justify-between">
              <dt class="text-text-muted">{{ t('returns.detail.subtotal') }}</dt>
              <dd class="tnum text-text">{{ formatMoney(doc.subTotal, currency) }}</dd>
            </div>
            <div class="flex justify-between">
              <dt class="text-text-muted">{{ t('returns.detail.taxTotal') }}</dt>
              <dd class="tnum text-text">{{ formatMoney(doc.taxTotal, currency) }}</dd>
            </div>
            <div class="flex justify-between border-t border-border pt-1.5 font-semibold">
              <dt class="text-text">{{ t('returns.detail.grandTotal') }}</dt>
              <dd class="tnum text-text">{{ formatMoney(doc.grandTotal, currency) }}</dd>
            </div>
            <div class="flex justify-between pt-1.5 text-xs">
              <dt class="text-text-muted">{{ t('returns.detail.destocked') }}</dt>
              <dd class="tnum text-text-muted">{{ formatMoney(doc.totalCost, currency) }}</dd>
            </div>
          </dl>
        </div>
      </AppCard>

      <AppCard v-if="doc.notes" :title="t('returns.detail.notes')">
        <p class="text-sm text-text">{{ doc.notes }}</p>
      </AppCard>
    </template>
  </div>
</template>
