<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { ArrowLeft, Wallet } from 'lucide-vue-next'
import { masterData } from '@/lib/masterData'
import { pricingApi } from '@/lib/pricing'
import { accountingApi } from '@/lib/accounting'
import { salesApi } from '@/lib/sales'
import { formatMoney, formatMoneyShort } from '@/lib/format'
import { useAuthStore } from '@/stores/auth'
import type { Customer } from '@/types/masterdata'
import type { SubledgerOpenItem } from '@/types/accounting'
import type { CustomerPaymentSummary, SalesOrderSummary } from '@/types/sales'
import AppButton from '@/components/ui/AppButton.vue'
import AppCard from '@/components/ui/AppCard.vue'
import InsightCards, { type Insight } from '@/components/ui/InsightCards.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const id = computed(() => String(route.params.id))
const canPay = computed(() => auth.has('Sales.Post'))

function receivePayment() {
  router.push({ name: 'salesReceivePayment', query: { customerId: id.value } })
}
const loading = ref(true)
const customer = ref<Customer | null>(null)
const priceListName = ref<string | null>(null)
const openItems = ref<SubledgerOpenItem[]>([])
const orders = ref<SalesOrderSummary[]>([])
const payments = ref<CustomerPaymentSummary[]>([])

const today = new Date().toISOString().slice(0, 10)
const isOverdue = (o: SubledgerOpenItem) => o.dueDate < today

const receivable = computed(() => openItems.value.reduce((s, o) => s + o.outstandingAmount, 0))
const overdue = computed(() =>
  openItems.value.filter(isOverdue).reduce((s, o) => s + o.outstandingAmount, 0),
)
const lifetimeSales = computed(() => orders.value.reduce((s, o) => s + o.grandTotal, 0))

const insights = computed<Insight[]>(() => [
  { label: t('masterData.customers.receivable'), value: formatMoneyShort(receivable.value), tone: 'accent' },
  { label: t('masterData.customers.overdue'), value: formatMoneyShort(overdue.value), tone: overdue.value > 0 ? 'negative' : 'neutral' },
  { label: t('party.orders'), value: String(orders.value.length) },
  { label: t('party.lifetime'), value: formatMoneyShort(lifetimeSales.value) },
])

async function load() {
  loading.value = true
  try {
    const [customers, lists] = await Promise.all([masterData.customers(), pricingApi.list().catch(() => [])])
    customer.value = customers.find((c) => c.id === id.value) ?? null
    priceListName.value = customer.value?.salesPriceListId
      ? lists.find((l) => l.id === customer.value?.salesPriceListId)?.name ?? null
      : null

    // Cross-module history; degrade gracefully if a permission is missing.
    const [ar, allOrders, pmts] = await Promise.all([
      accountingApi.arOpenItems(id.value).catch(() => []),
      salesApi.list().catch(() => []),
      salesApi.customerPayments(id.value).catch(() => []),
    ])
    openItems.value = ar
    orders.value = allOrders.filter((o) => o.customerId === id.value)
    payments.value = pmts
  } finally {
    loading.value = false
  }
}
onMounted(load)

function openOrder(orderId: string) {
  router.push({ name: 'salesOrderDetail', params: { id: orderId } })
}
</script>

<template>
  <div class="space-y-5">
    <button class="inline-flex items-center gap-1.5 text-sm text-text-muted hover:text-text" @click="router.push({ name: 'masterDataCustomers' })">
      <ArrowLeft :size="16" /> {{ t('party.backToCustomers') }}
    </button>

    <p v-if="loading" class="text-sm text-text-muted">{{ t('common.loading') }}</p>
    <p v-else-if="!customer" class="text-sm text-text-muted">{{ t('party.notFound') }}</p>

    <template v-else>
      <div class="flex flex-wrap items-start justify-between gap-4">
        <div>
          <div class="flex items-center gap-3">
            <h2 class="text-xl font-semibold text-text">{{ customer.name }}</h2>
            <StatusBadge :label="customer.isActive ? t('masterData.active') : t('masterData.inactive')" :tone="customer.isActive ? 'positive' : 'neutral'" />
          </div>
          <p class="mt-1 text-sm text-text-muted">{{ customer.code }}</p>
        </div>
        <AppButton v-if="canPay && receivable > 0" @click="receivePayment">
          <Wallet :size="16" /> {{ t('party.receivePayment') }}
        </AppButton>
      </div>

      <InsightCards :items="insights" />

      <AppCard :title="t('party.profile')">
        <dl class="grid grid-cols-2 gap-4 text-sm sm:grid-cols-4">
          <div><dt class="text-xs text-text-muted">{{ t('masterData.fields.taxId') }}</dt><dd class="text-text">{{ customer.taxId || '—' }}</dd></div>
          <div><dt class="text-xs text-text-muted">{{ t('masterData.fields.terms') }}</dt><dd class="text-text">{{ customer.paymentTermDays }} {{ t('party.days') }}</dd></div>
          <div><dt class="text-xs text-text-muted">{{ t('masterData.fields.creditLimit') }}</dt><dd class="text-text tnum">{{ formatMoney(customer.creditLimit) }}</dd></div>
          <div><dt class="text-xs text-text-muted">{{ t('priceLists.assign.sales') }}</dt><dd class="text-text">{{ priceListName || t('priceLists.assign.none') }}</dd></div>
        </dl>
      </AppCard>

      <!-- Open invoices (what they still owe) -->
      <AppCard :title="t('party.openInvoices')" :padded="false">
        <table v-if="openItems.length" class="w-full text-sm">
          <thead>
            <tr class="border-b border-border text-xs uppercase tracking-wide text-text-muted">
              <th class="px-4 py-2.5 text-left font-semibold">{{ t('party.document') }}</th>
              <th class="px-3 py-2.5 text-left font-semibold">{{ t('party.date') }}</th>
              <th class="px-3 py-2.5 text-left font-semibold">{{ t('party.due') }}</th>
              <th class="px-4 py-2.5 text-right font-semibold">{{ t('party.outstanding') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="o in openItems" :key="o.id" class="border-b border-border last:border-0">
              <td class="px-4 py-2.5 text-text">{{ o.documentNo }}</td>
              <td class="px-3 py-2.5 text-text-muted">{{ o.documentDate }}</td>
              <td class="px-3 py-2.5" :class="isOverdue(o) ? 'text-negative' : 'text-text-muted'">{{ o.dueDate }}</td>
              <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(o.outstandingAmount) }}</td>
            </tr>
          </tbody>
        </table>
        <p v-else class="px-4 py-6 text-center text-sm text-text-muted">{{ t('party.noOpenInvoices') }}</p>
      </AppCard>

      <div class="grid grid-cols-1 gap-5 lg:grid-cols-2">
        <!-- Sales orders (their purchases from us) -->
        <AppCard :title="t('party.salesOrders')" :padded="false">
          <table v-if="orders.length" class="w-full text-sm">
            <tbody>
              <tr v-for="o in orders" :key="o.id" class="cursor-pointer border-b border-border last:border-0 hover:bg-surface-2" @click="openOrder(o.id)">
                <td class="px-4 py-2.5 text-text">{{ o.number }}</td>
                <td class="px-3 py-2.5 text-text-muted">{{ o.orderDate }}</td>
                <td class="px-3 py-2.5"><StatusBadge :status="o.status" :label="t(`sales.status.${o.status}`)" /></td>
                <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(o.grandTotal) }}</td>
              </tr>
            </tbody>
          </table>
          <p v-else class="px-4 py-6 text-center text-sm text-text-muted">{{ t('party.noOrders') }}</p>
        </AppCard>

        <!-- Payments received -->
        <AppCard :title="t('party.paymentsReceived')" :padded="false">
          <table v-if="payments.length" class="w-full text-sm">
            <tbody>
              <tr v-for="p in payments" :key="p.id" class="border-b border-border last:border-0">
                <td class="px-4 py-2.5 text-text">{{ p.number }}</td>
                <td class="px-3 py-2.5 text-text-muted">{{ p.paymentDate }}</td>
                <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(p.totalAmount) }}</td>
              </tr>
            </tbody>
          </table>
          <p v-else class="px-4 py-6 text-center text-sm text-text-muted">{{ t('party.noPayments') }}</p>
        </AppCard>
      </div>
    </template>
  </div>
</template>
