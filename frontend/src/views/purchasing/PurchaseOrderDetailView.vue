<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { ArrowLeft } from 'lucide-vue-next'
import { purchasingApi } from '@/lib/purchasing'
import { masterData, nameMap } from '@/lib/masterData'
import { formatMoney, formatNumber, formatPercent } from '@/lib/format'
import type { GoodsReceiptSummary, PurchaseInvoiceSummary, PurchaseOrder } from '@/types/purchasing'
import AppButton from '@/components/ui/AppButton.vue'
import AppCard from '@/components/ui/AppCard.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()

const order = ref<PurchaseOrder | null>(null)
const receipts = ref<GoodsReceiptSummary[]>([])
const invoices = ref<PurchaseInvoiceSummary[]>([])
const products = ref(new Map<string, string>())
const suppliers = ref(new Map<string, string>())
const loading = ref(true)
const busy = ref<'' | 'submit' | 'receive' | 'invoice'>('')
const error = ref('')

const id = computed(() => String(route.params.id))
const currency = computed(() => order.value?.currency ?? 'IDR')
const today = () => new Date().toISOString().slice(0, 10)
const inDays = (n: number) => new Date(Date.now() + n * 86_400_000).toISOString().slice(0, 10)

async function load() {
  loading.value = true
  try {
    const [o, prods, sups, recs, invs] = await Promise.all([
      purchasingApi.get(id.value),
      masterData.products(),
      masterData.suppliers(),
      purchasingApi.receipts(id.value),
      purchasingApi.invoices(id.value),
    ])
    order.value = o
    products.value = nameMap(prods)
    suppliers.value = nameMap(sups)
    receipts.value = recs
    invoices.value = invs
  } finally {
    loading.value = false
  }
}

const canReceive = computed(
  () => order.value?.status === 'Approved' || order.value?.status === 'PartiallyReceived',
)
const hasOutstanding = computed(() => order.value?.lines.some((l) => l.outstandingQuantity > 0) ?? false)
const hasUninvoiced = computed(
  () => order.value?.lines.some((l) => l.receivedQuantity - l.invoicedQuantity > 0) ?? false,
)

async function run(kind: 'submit' | 'receive' | 'invoice', fn: () => Promise<unknown>) {
  if (!order.value) return
  busy.value = kind
  error.value = ''
  try {
    await fn()
    await load()
  } catch {
    error.value = t('purchasing.detail.actionFailed')
  } finally {
    busy.value = ''
  }
}

const submit = () => run('submit', () => purchasingApi.submit(order.value!.id))

const receive = () =>
  run('receive', () =>
    purchasingApi.createReceipt(order.value!.id, {
      receiptDate: today(),
      notes: null,
      lines: order.value!.lines
        .filter((l) => l.outstandingQuantity > 0)
        .map((l) => ({ purchaseOrderLineId: l.id, quantity: l.outstandingQuantity })),
    }),
  )

const invoice = () =>
  run('invoice', () =>
    purchasingApi.createInvoice(order.value!.id, {
      supplierInvoiceNo: null,
      invoiceDate: today(),
      dueDate: inDays(30),
      notes: null,
      lines: order.value!.lines
        .filter((l) => l.receivedQuantity - l.invoicedQuantity > 0)
        .map((l) => ({ purchaseOrderLineId: l.id, quantity: l.receivedQuantity - l.invoicedQuantity })),
    }),
  )

onMounted(load)
</script>

<template>
  <div class="space-y-5">
    <button
      class="inline-flex items-center gap-1.5 text-sm text-text-muted hover:text-text"
      @click="router.push({ name: 'purchasing' })"
    >
      <ArrowLeft :size="16" /> {{ t('purchasing.backToList') }}
    </button>

    <p v-if="loading" class="text-sm text-text-muted">{{ t('common.loading') }}</p>

    <template v-else-if="order">
      <div class="flex flex-wrap items-start justify-between gap-4">
        <div>
          <div class="flex items-center gap-3">
            <h2 class="text-xl font-semibold text-text">{{ order.number }}</h2>
            <StatusBadge :status="order.status" :label="t(`purchasing.status.${order.status}`)" />
          </div>
          <p class="mt-1 text-sm text-text-muted">
            {{ suppliers.get(order.supplierId) ?? '—' }} · {{ order.orderDate }}
          </p>
        </div>
        <div class="flex flex-wrap items-center gap-2">
          <AppButton v-if="order.status === 'Draft'" :disabled="busy !== ''" @click="submit">
            {{ busy === 'submit' ? t('purchasing.detail.submitting') : t('purchasing.detail.submit') }}
          </AppButton>
          <AppButton v-if="canReceive && hasOutstanding" variant="secondary" :disabled="busy !== ''" @click="receive">
            {{ busy === 'receive' ? t('purchasing.detail.receiving') : t('purchasing.detail.receive') }}
          </AppButton>
          <AppButton v-if="hasUninvoiced" :disabled="busy !== ''" @click="invoice">
            {{ busy === 'invoice' ? t('purchasing.detail.invoicing') : t('purchasing.detail.createInvoice') }}
          </AppButton>
        </div>
      </div>

      <p v-if="error" class="text-sm text-negative">{{ error }}</p>

      <AppCard :title="t('purchasing.detail.lines')" :padded="false">
        <div class="overflow-x-auto">
          <table class="w-full text-sm">
            <thead>
              <tr class="border-b border-border text-xs uppercase tracking-wide text-text-muted">
                <th class="px-4 py-2.5 text-left font-semibold">{{ t('purchasing.detail.product') }}</th>
                <th class="px-3 py-2.5 text-right font-semibold">{{ t('purchasing.detail.ordered') }}</th>
                <th class="px-3 py-2.5 text-right font-semibold">{{ t('purchasing.detail.unitPrice') }}</th>
                <th class="px-3 py-2.5 text-right font-semibold">{{ t('purchasing.detail.taxPct') }}</th>
                <th class="px-3 py-2.5 text-right font-semibold">{{ t('purchasing.detail.received') }}</th>
                <th class="px-3 py-2.5 text-right font-semibold">{{ t('purchasing.detail.invoiced') }}</th>
                <th class="px-4 py-2.5 text-right font-semibold">{{ t('purchasing.detail.lineTotal') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="line in order.lines" :key="line.id" class="border-b border-border last:border-0">
                <td class="px-4 py-3 text-text">{{ products.get(line.productId) ?? '—' }}</td>
                <td class="px-3 py-3 text-right text-text tnum">{{ formatNumber(line.quantity) }}</td>
                <td class="px-3 py-3 text-right text-text tnum">{{ formatMoney(line.unitPrice, currency) }}</td>
                <td class="px-3 py-3 text-right text-text-muted tnum">{{ formatPercent(line.taxRate * 100) }}</td>
                <td class="px-3 py-3 text-right text-text-muted tnum">{{ formatNumber(line.receivedQuantity) }}</td>
                <td class="px-3 py-3 text-right text-text-muted tnum">{{ formatNumber(line.invoicedQuantity) }}</td>
                <td class="px-4 py-3 text-right text-text tnum">{{ formatMoney(line.lineTotal, currency) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <div class="flex justify-end border-t border-border p-4">
          <dl class="w-full max-w-xs space-y-1.5 text-sm">
            <div class="flex justify-between"><dt class="text-text-muted">{{ t('purchasing.detail.subtotal') }}</dt><dd class="tnum text-text">{{ formatMoney(order.subTotal, currency) }}</dd></div>
            <div class="flex justify-between"><dt class="text-text-muted">{{ t('purchasing.detail.taxTotal') }}</dt><dd class="tnum text-text">{{ formatMoney(order.taxTotal, currency) }}</dd></div>
            <div class="flex justify-between border-t border-border pt-1.5 font-semibold"><dt class="text-text">{{ t('purchasing.detail.grandTotal') }}</dt><dd class="tnum text-text">{{ formatMoney(order.grandTotal, currency) }}</dd></div>
          </dl>
        </div>
      </AppCard>

      <div class="grid grid-cols-1 gap-5 lg:grid-cols-2">
        <AppCard :title="t('purchasing.detail.receipts')" :padded="false">
          <table v-if="receipts.length" class="w-full text-sm">
            <tbody>
              <tr v-for="r in receipts" :key="r.id" class="border-b border-border last:border-0">
                <td class="px-4 py-2.5 text-text">{{ r.number }}</td>
                <td class="px-4 py-2.5 text-text-muted">{{ r.receiptDate }}</td>
                <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(r.totalCost, currency) }}</td>
                <td class="px-4 py-2.5 text-right"><StatusBadge v-if="r.journalEntryId" tone="positive" :label="t('purchasing.detail.posted')" /></td>
              </tr>
            </tbody>
          </table>
          <p v-else class="px-4 py-6 text-center text-sm text-text-muted">{{ t('purchasing.detail.noReceipts') }}</p>
        </AppCard>

        <AppCard :title="t('purchasing.detail.invoices')" :padded="false">
          <table v-if="invoices.length" class="w-full text-sm">
            <tbody>
              <tr v-for="inv in invoices" :key="inv.id" class="border-b border-border last:border-0">
                <td class="px-4 py-2.5 text-text">{{ inv.number }}</td>
                <td class="px-4 py-2.5 text-text-muted">{{ t('purchasing.detail.due') }} {{ inv.dueDate }}</td>
                <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(inv.grandTotal, currency) }}</td>
                <td class="px-4 py-2.5 text-right"><StatusBadge v-if="inv.journalEntryId" tone="positive" :label="t('purchasing.detail.posted')" /></td>
              </tr>
            </tbody>
          </table>
          <p v-else class="px-4 py-6 text-center text-sm text-text-muted">{{ t('purchasing.detail.noInvoices') }}</p>
        </AppCard>
      </div>

      <AppCard v-if="order.notes" :title="t('purchasing.detail.notes')">
        <p class="text-sm text-text">{{ order.notes }}</p>
      </AppCard>
    </template>
  </div>
</template>
