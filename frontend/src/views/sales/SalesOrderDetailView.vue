<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { ArrowLeft } from 'lucide-vue-next'
import { salesApi } from '@/lib/sales'
import { masterData, nameMap } from '@/lib/masterData'
import { formatMoney, formatNumber, formatPercent } from '@/lib/format'
import type { DeliverySummary, SalesInvoiceSummary, SalesOrder } from '@/types/sales'
import AppButton from '@/components/ui/AppButton.vue'
import AppCard from '@/components/ui/AppCard.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()

const order = ref<SalesOrder | null>(null)
const deliveries = ref<DeliverySummary[]>([])
const invoices = ref<SalesInvoiceSummary[]>([])
const products = ref(new Map<string, string>())
const customers = ref(new Map<string, string>())
const loading = ref(true)
const busy = ref<'' | 'submit' | 'deliver' | 'invoice'>('')
const error = ref('')

const id = computed(() => String(route.params.id))
const currency = computed(() => order.value?.currency ?? 'IDR')

const today = () => new Date().toISOString().slice(0, 10)
const inDays = (n: number) => new Date(Date.now() + n * 86_400_000).toISOString().slice(0, 10)

async function load() {
  loading.value = true
  try {
    const [o, prods, custs, dels, invs] = await Promise.all([
      salesApi.get(id.value),
      masterData.products(),
      masterData.customers(),
      salesApi.deliveries(id.value),
      salesApi.invoices(id.value),
    ])
    order.value = o
    products.value = nameMap(prods)
    customers.value = nameMap(custs)
    deliveries.value = dels
    invoices.value = invs
  } finally {
    loading.value = false
  }
}

const canDeliver = computed(
  () => order.value?.status === 'Approved' || order.value?.status === 'PartiallyDelivered',
)
const hasOutstanding = computed(() => order.value?.lines.some((l) => l.outstandingQuantity > 0) ?? false)
const hasUninvoiced = computed(
  () => order.value?.lines.some((l) => l.deliveredQuantity - l.invoicedQuantity > 0) ?? false,
)

async function run(kind: 'submit' | 'deliver' | 'invoice', fn: () => Promise<unknown>) {
  if (!order.value) return
  busy.value = kind
  error.value = ''
  try {
    await fn()
    await load()
  } catch {
    error.value = t('sales.detail.actionFailed')
  } finally {
    busy.value = ''
  }
}

const submit = () => run('submit', () => salesApi.submit(order.value!.id))

const deliver = () =>
  run('deliver', () =>
    salesApi.createDelivery(order.value!.id, {
      deliveryDate: today(),
      notes: null,
      lines: order.value!.lines
        .filter((l) => l.outstandingQuantity > 0)
        .map((l) => ({ salesOrderLineId: l.id, quantity: l.outstandingQuantity })),
    }),
  )

const invoice = () =>
  run('invoice', () =>
    salesApi.createInvoice(order.value!.id, {
      invoiceDate: today(),
      dueDate: inDays(30),
      notes: null,
      lines: order.value!.lines
        .filter((l) => l.deliveredQuantity - l.invoicedQuantity > 0)
        .map((l) => ({ salesOrderLineId: l.id, quantity: l.deliveredQuantity - l.invoicedQuantity })),
    }),
  )

onMounted(load)
</script>

<template>
  <div class="space-y-5">
    <button
      class="inline-flex items-center gap-1.5 text-sm text-text-muted hover:text-text"
      @click="router.push({ name: 'sales' })"
    >
      <ArrowLeft :size="16" /> {{ t('sales.backToList') }}
    </button>

    <p v-if="loading" class="text-sm text-text-muted">{{ t('common.loading') }}</p>

    <template v-else-if="order">
      <!-- Header + actions -->
      <div class="flex flex-wrap items-start justify-between gap-4">
        <div>
          <div class="flex items-center gap-3">
            <h2 class="text-xl font-semibold text-text">{{ order.number }}</h2>
            <StatusBadge :status="order.status" :label="t(`sales.status.${order.status}`)" />
          </div>
          <p class="mt-1 text-sm text-text-muted">
            {{ customers.get(order.customerId) ?? '—' }} · {{ order.orderDate }}
          </p>
        </div>
        <div class="flex flex-wrap items-center gap-2">
          <AppButton v-if="order.status === 'Draft'" :disabled="busy !== ''" @click="submit">
            {{ busy === 'submit' ? t('sales.detail.submitting') : t('sales.detail.submit') }}
          </AppButton>
          <AppButton
            v-if="canDeliver && hasOutstanding"
            variant="secondary"
            :disabled="busy !== ''"
            @click="deliver"
          >
            {{ busy === 'deliver' ? t('sales.detail.delivering') : t('sales.detail.deliverOutstanding') }}
          </AppButton>
          <AppButton v-if="hasUninvoiced" :disabled="busy !== ''" @click="invoice">
            {{ busy === 'invoice' ? t('sales.detail.invoicing') : t('sales.detail.createInvoice') }}
          </AppButton>
        </div>
      </div>

      <p v-if="error" class="text-sm text-negative">{{ error }}</p>

      <!-- Lines -->
      <AppCard :title="t('sales.detail.lines')" :padded="false">
        <div class="overflow-x-auto">
          <table class="w-full text-sm">
            <thead>
              <tr class="border-b border-border text-xs uppercase tracking-wide text-text-muted">
                <th class="px-4 py-2.5 text-left font-semibold">{{ t('sales.detail.product') }}</th>
                <th class="px-3 py-2.5 text-right font-semibold">{{ t('sales.detail.ordered') }}</th>
                <th class="px-3 py-2.5 text-right font-semibold">{{ t('sales.detail.unitPrice') }}</th>
                <th class="px-3 py-2.5 text-right font-semibold">{{ t('sales.detail.taxPct') }}</th>
                <th class="px-3 py-2.5 text-right font-semibold">{{ t('sales.detail.delivered') }}</th>
                <th class="px-3 py-2.5 text-right font-semibold">{{ t('sales.detail.invoiced') }}</th>
                <th class="px-4 py-2.5 text-right font-semibold">{{ t('sales.detail.lineTotal') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="line in order.lines" :key="line.id" class="border-b border-border last:border-0">
                <td class="px-4 py-3 text-text">{{ products.get(line.productId) ?? '—' }}</td>
                <td class="px-3 py-3 text-right text-text tnum">{{ formatNumber(line.quantity) }}</td>
                <td class="px-3 py-3 text-right text-text tnum">{{ formatMoney(line.unitPrice, currency) }}</td>
                <td class="px-3 py-3 text-right text-text-muted tnum">{{ formatPercent(line.taxRate * 100) }}</td>
                <td class="px-3 py-3 text-right text-text-muted tnum">{{ formatNumber(line.deliveredQuantity) }}</td>
                <td class="px-3 py-3 text-right text-text-muted tnum">{{ formatNumber(line.invoicedQuantity) }}</td>
                <td class="px-4 py-3 text-right text-text tnum">{{ formatMoney(line.lineTotal, currency) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <div class="flex justify-end border-t border-border p-4">
          <dl class="w-full max-w-xs space-y-1.5 text-sm">
            <div class="flex justify-between">
              <dt class="text-text-muted">{{ t('sales.detail.subtotal') }}</dt>
              <dd class="tnum text-text">{{ formatMoney(order.subTotal, currency) }}</dd>
            </div>
            <div class="flex justify-between">
              <dt class="text-text-muted">{{ t('sales.detail.taxTotal') }}</dt>
              <dd class="tnum text-text">{{ formatMoney(order.taxTotal, currency) }}</dd>
            </div>
            <div class="flex justify-between border-t border-border pt-1.5 font-semibold">
              <dt class="text-text">{{ t('sales.detail.grandTotal') }}</dt>
              <dd class="tnum text-text">{{ formatMoney(order.grandTotal, currency) }}</dd>
            </div>
          </dl>
        </div>
      </AppCard>

      <!-- Related documents -->
      <div class="grid grid-cols-1 gap-5 lg:grid-cols-2">
        <AppCard :title="t('sales.detail.deliveries')" :padded="false">
          <table v-if="deliveries.length" class="w-full text-sm">
            <tbody>
              <tr v-for="d in deliveries" :key="d.id" class="border-b border-border last:border-0">
                <td class="px-4 py-2.5 text-text">{{ d.number }}</td>
                <td class="px-4 py-2.5 text-text-muted">{{ d.deliveryDate }}</td>
                <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(d.totalCost, currency) }}</td>
                <td class="px-4 py-2.5 text-right">
                  <StatusBadge v-if="d.journalEntryId" tone="positive" :label="t('sales.detail.posted')" />
                </td>
              </tr>
            </tbody>
          </table>
          <p v-else class="px-4 py-6 text-center text-sm text-text-muted">{{ t('sales.detail.noDeliveries') }}</p>
        </AppCard>

        <AppCard :title="t('sales.detail.invoices')" :padded="false">
          <table v-if="invoices.length" class="w-full text-sm">
            <tbody>
              <tr v-for="inv in invoices" :key="inv.id" class="border-b border-border last:border-0">
                <td class="px-4 py-2.5 text-text">{{ inv.number }}</td>
                <td class="px-4 py-2.5 text-text-muted">{{ t('sales.detail.due') }} {{ inv.dueDate }}</td>
                <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(inv.grandTotal, currency) }}</td>
                <td class="px-4 py-2.5 text-right">
                  <StatusBadge v-if="inv.journalEntryId" tone="positive" :label="t('sales.detail.posted')" />
                </td>
              </tr>
            </tbody>
          </table>
          <p v-else class="px-4 py-6 text-center text-sm text-text-muted">{{ t('sales.detail.noInvoices') }}</p>
        </AppCard>
      </div>

      <AppCard v-if="order.notes" :title="t('sales.detail.notes')">
        <p class="text-sm text-text">{{ order.notes }}</p>
      </AppCard>
    </template>
  </div>
</template>
