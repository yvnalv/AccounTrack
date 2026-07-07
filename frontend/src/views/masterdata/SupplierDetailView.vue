<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { ArrowLeft } from 'lucide-vue-next'
import { masterData } from '@/lib/masterData'
import { pricingApi } from '@/lib/pricing'
import { accountingApi } from '@/lib/accounting'
import { purchasingApi } from '@/lib/purchasing'
import { formatMoney, formatMoneyShort } from '@/lib/format'
import type { Supplier } from '@/types/masterdata'
import type { SubledgerOpenItem } from '@/types/accounting'
import type { PurchaseOrderSummary, SupplierPaymentSummary } from '@/types/purchasing'
import AppCard from '@/components/ui/AppCard.vue'
import InsightCards, { type Insight } from '@/components/ui/InsightCards.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()

const id = computed(() => String(route.params.id))
const loading = ref(true)
const supplier = ref<Supplier | null>(null)
const priceListName = ref<string | null>(null)
const openItems = ref<SubledgerOpenItem[]>([])
const orders = ref<PurchaseOrderSummary[]>([])
const payments = ref<SupplierPaymentSummary[]>([])

const today = new Date().toISOString().slice(0, 10)
const isOverdue = (o: SubledgerOpenItem) => o.dueDate < today

const payable = computed(() => openItems.value.reduce((s, o) => s + o.outstandingAmount, 0))
const overdue = computed(() =>
  openItems.value.filter(isOverdue).reduce((s, o) => s + o.outstandingAmount, 0),
)
const lifetimePurchases = computed(() => orders.value.reduce((s, o) => s + o.grandTotal, 0))

const insights = computed<Insight[]>(() => [
  { label: t('masterData.suppliers.payable'), value: formatMoneyShort(payable.value), tone: 'accent' },
  { label: t('masterData.suppliers.overdue'), value: formatMoneyShort(overdue.value), tone: overdue.value > 0 ? 'negative' : 'neutral' },
  { label: t('party.purchaseOrdersCount'), value: String(orders.value.length) },
  { label: t('party.lifetimePurchases'), value: formatMoneyShort(lifetimePurchases.value) },
])

async function load() {
  loading.value = true
  try {
    const [suppliers, lists] = await Promise.all([masterData.suppliers(), pricingApi.list().catch(() => [])])
    supplier.value = suppliers.find((s) => s.id === id.value) ?? null
    priceListName.value = supplier.value?.purchasePriceListId
      ? lists.find((l) => l.id === supplier.value?.purchasePriceListId)?.name ?? null
      : null

    const [ap, allOrders, pmts] = await Promise.all([
      accountingApi.apOpenItems(id.value).catch(() => []),
      purchasingApi.list().catch(() => []),
      purchasingApi.supplierPayments(id.value).catch(() => []),
    ])
    openItems.value = ap
    orders.value = allOrders.filter((o) => o.supplierId === id.value)
    payments.value = pmts
  } finally {
    loading.value = false
  }
}
onMounted(load)

function openOrder(orderId: string) {
  router.push({ name: 'purchaseOrderDetail', params: { id: orderId } })
}
</script>

<template>
  <div class="space-y-5">
    <button class="inline-flex items-center gap-1.5 text-sm text-text-muted hover:text-text" @click="router.push({ name: 'masterDataSuppliers' })">
      <ArrowLeft :size="16" /> {{ t('party.backToSuppliers') }}
    </button>

    <p v-if="loading" class="text-sm text-text-muted">{{ t('common.loading') }}</p>
    <p v-else-if="!supplier" class="text-sm text-text-muted">{{ t('party.notFound') }}</p>

    <template v-else>
      <div class="flex flex-wrap items-start justify-between gap-4">
        <div>
          <div class="flex items-center gap-3">
            <h2 class="text-xl font-semibold text-text">{{ supplier.name }}</h2>
            <StatusBadge :label="supplier.isActive ? t('masterData.active') : t('masterData.inactive')" :tone="supplier.isActive ? 'positive' : 'neutral'" />
          </div>
          <p class="mt-1 text-sm text-text-muted">{{ supplier.code }}</p>
        </div>
      </div>

      <InsightCards :items="insights" />

      <AppCard :title="t('party.profile')">
        <dl class="grid grid-cols-2 gap-4 text-sm sm:grid-cols-3">
          <div><dt class="text-xs text-text-muted">{{ t('masterData.fields.taxId') }}</dt><dd class="text-text">{{ supplier.taxId || '—' }}</dd></div>
          <div><dt class="text-xs text-text-muted">{{ t('masterData.fields.terms') }}</dt><dd class="text-text">{{ supplier.paymentTermDays }} {{ t('party.days') }}</dd></div>
          <div><dt class="text-xs text-text-muted">{{ t('priceLists.assign.purchase') }}</dt><dd class="text-text">{{ priceListName || t('priceLists.assign.none') }}</dd></div>
        </dl>
      </AppCard>

      <!-- Open bills (what we still owe) -->
      <AppCard :title="t('party.openBills')" :padded="false">
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
        <p v-else class="px-4 py-6 text-center text-sm text-text-muted">{{ t('party.noOpenBills') }}</p>
      </AppCard>

      <div class="grid grid-cols-1 gap-5 lg:grid-cols-2">
        <!-- Purchase orders (our purchases from them) -->
        <AppCard :title="t('party.purchaseOrders')" :padded="false">
          <table v-if="orders.length" class="w-full text-sm">
            <tbody>
              <tr v-for="o in orders" :key="o.id" class="cursor-pointer border-b border-border last:border-0 hover:bg-surface-2" @click="openOrder(o.id)">
                <td class="px-4 py-2.5 text-text">{{ o.number }}</td>
                <td class="px-3 py-2.5 text-text-muted">{{ o.orderDate }}</td>
                <td class="px-3 py-2.5"><StatusBadge :status="o.status" :label="t(`purchasing.status.${o.status}`)" /></td>
                <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(o.grandTotal) }}</td>
              </tr>
            </tbody>
          </table>
          <p v-else class="px-4 py-6 text-center text-sm text-text-muted">{{ t('party.noPurchaseOrders') }}</p>
        </AppCard>

        <!-- Payments made -->
        <AppCard :title="t('party.paymentsMade')" :padded="false">
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
