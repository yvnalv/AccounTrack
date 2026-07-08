<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { ArrowLeft, ExternalLink, FileText } from 'lucide-vue-next'
import { salesApi } from '@/lib/sales'
import { masterData, nameMap } from '@/lib/masterData'
import { downloadFile } from '@/lib/api'
import { formatMoney, formatNumber, formatPercent } from '@/lib/format'
import type { SalesInvoice } from '@/types/sales'
import AppCard from '@/components/ui/AppCard.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()

const doc = ref<SalesInvoice | null>(null)
const products = ref(new Map<string, string>())
const customers = ref(new Map<string, string>())
const loading = ref(true)

const id = computed(() => String(route.params.id))
const currency = computed(() => doc.value?.currency ?? 'IDR')

async function load() {
  loading.value = true
  try {
    const [inv, prods, custs] = await Promise.all([
      salesApi.getInvoice(id.value),
      masterData.products(),
      masterData.customers(),
    ])
    doc.value = inv
    products.value = nameMap(prods)
    customers.value = nameMap(custs)
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
      @click="doc ? router.push({ name: 'salesOrderDetail', params: { id: doc.salesOrderId } }) : router.back()"
    >
      <ArrowLeft :size="16" /> {{ t('docDetail.backToSalesOrder') }}
    </button>

    <p v-if="loading" class="text-sm text-text-muted">{{ t('docDetail.loading') }}</p>
    <p v-else-if="!doc" class="text-sm text-text-muted">{{ t('docDetail.notFound') }}</p>

    <template v-else>
      <div class="flex flex-wrap items-start justify-between gap-4">
        <div>
          <div class="flex items-center gap-3">
            <h2 class="text-xl font-semibold text-text">{{ doc.number }}</h2>
            <StatusBadge tone="info" :label="t('docDetail.invoiceTitle')" />
            <StatusBadge
              :tone="doc.journalEntryId ? 'positive' : 'neutral'"
              :label="doc.journalEntryId ? t('docDetail.posted') : t('docDetail.notPosted')"
            />
          </div>
          <p class="mt-1 text-sm text-text-muted">
            {{ customers.get(doc.customerId) ?? '—' }} · {{ doc.invoiceDate }} ·
            {{ t('docDetail.dueDate') }} {{ doc.dueDate }}
          </p>
        </div>
        <div class="flex flex-wrap items-center gap-2">
          <button
            class="inline-flex items-center gap-1.5 rounded-button border border-border px-3 py-2 text-sm font-medium text-text-muted transition-colors hover:bg-surface-2 hover:text-text"
            @click="downloadFile(`/sales-invoices/${doc.id}/pdf`, `invoice-${doc.number}.pdf`)"
          >
            <FileText :size="16" /> {{ t('docDetail.pdf') }}
          </button>
          <button
            class="inline-flex items-center gap-1.5 rounded-button border border-border px-3 py-2 text-sm font-medium text-text-muted transition-colors hover:bg-surface-2 hover:text-text"
            @click="router.push({ name: 'salesOrderDetail', params: { id: doc.salesOrderId } })"
          >
            <ExternalLink :size="16" /> {{ t('docDetail.viewSalesOrder') }}
          </button>
        </div>
      </div>

      <AppCard :title="t('docDetail.lines')" :padded="false">
        <div class="overflow-x-auto">
          <table class="w-full text-sm">
            <thead>
              <tr class="border-b border-border text-xs uppercase tracking-wide text-text-muted">
                <th class="px-4 py-2.5 text-left font-semibold">{{ t('docDetail.product') }}</th>
                <th class="px-3 py-2.5 text-right font-semibold">{{ t('docDetail.qty') }}</th>
                <th class="px-3 py-2.5 text-right font-semibold">{{ t('docDetail.unitPrice') }}</th>
                <th class="px-3 py-2.5 text-right font-semibold">{{ t('docDetail.taxPct') }}</th>
                <th class="px-4 py-2.5 text-right font-semibold">{{ t('docDetail.lineTotal') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="line in doc.lines" :key="line.id" class="border-b border-border last:border-0">
                <td class="px-4 py-3 text-text">{{ products.get(line.productId) ?? '—' }}</td>
                <td class="px-3 py-3 text-right text-text tnum">{{ formatNumber(line.quantity) }}</td>
                <td class="px-3 py-3 text-right text-text tnum">{{ formatMoney(line.unitPrice, currency) }}</td>
                <td class="px-3 py-3 text-right text-text-muted tnum">{{ formatPercent(line.taxRate * 100) }}</td>
                <td class="px-4 py-3 text-right text-text tnum">{{ formatMoney(line.lineTotal, currency) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <div class="flex justify-end border-t border-border p-4">
          <dl class="w-full max-w-xs space-y-1.5 text-sm">
            <div class="flex justify-between">
              <dt class="text-text-muted">{{ t('docDetail.subtotal') }}</dt>
              <dd class="tnum text-text">{{ formatMoney(doc.subTotal, currency) }}</dd>
            </div>
            <div class="flex justify-between">
              <dt class="text-text-muted">{{ t('docDetail.taxTotal') }}</dt>
              <dd class="tnum text-text">{{ formatMoney(doc.taxTotal, currency) }}</dd>
            </div>
            <div class="flex justify-between border-t border-border pt-1.5 font-semibold">
              <dt class="text-text">{{ t('docDetail.grandTotal') }}</dt>
              <dd class="tnum text-text">{{ formatMoney(doc.grandTotal, currency) }}</dd>
            </div>
          </dl>
        </div>
      </AppCard>

      <AppCard v-if="doc.notes" :title="t('docDetail.notes')">
        <p class="text-sm text-text">{{ doc.notes }}</p>
      </AppCard>
    </template>
  </div>
</template>
