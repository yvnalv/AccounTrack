<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { ArrowLeft, Undo2, FileText, Pencil } from 'lucide-vue-next'
import { salesApi } from '@/lib/sales'
import { accountingApi, cashAccounts } from '@/lib/accounting'
import { useAuthStore } from '@/stores/auth'
import { downloadFile } from '@/lib/api'
import { masterData, nameMap } from '@/lib/masterData'
import { formatMoney, formatNumber, formatPercent } from '@/lib/format'
import type {
  DeliverySummary,
  SalesInvoiceSummary,
  SalesOrder,
  SalesReturnSummary,
} from '@/types/sales'
import type { AccountRef } from '@/types/accounting'
import AppButton from '@/components/ui/AppButton.vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppModal from '@/components/ui/AppModal.vue'
import AppSelect from '@/components/ui/AppSelect.vue'
import FormField from '@/components/ui/FormField.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import DocumentTimeline from '@/components/DocumentTimeline.vue'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const order = ref<SalesOrder | null>(null)
const deliveries = ref<DeliverySummary[]>([])
const invoices = ref<SalesInvoiceSummary[]>([])
const returns = ref<SalesReturnSummary[]>([])
const products = ref(new Map<string, string>())
const customers = ref(new Map<string, string>())
const loading = ref(true)
const busy = ref<'' | 'submit' | 'deliver' | 'invoice' | 'cancel'>('')
const error = ref('')

interface ReturnRow {
  salesInvoiceLineId: string
  productId: string
  returnable: number
  qty: number
}
const returnModal = ref(false)
const returnInvoiceNo = ref('')
const returnInvoiceId = ref('')
const returnRows = ref<ReturnRow[]>([])
const returnSaving = ref(false)
const returnError = ref('')
const refundAccountId = ref('')
const accounts = ref<AccountRef[]>([])
const cashOptions = computed(() =>
  cashAccounts(accounts.value).map((a) => ({ value: a.id, label: `${a.code} — ${a.name}` })),
)

const id = computed(() => String(route.params.id))
const currency = computed(() => order.value?.currency ?? 'IDR')

const today = () => new Date().toISOString().slice(0, 10)
const inDays = (n: number) => new Date(Date.now() + n * 86_400_000).toISOString().slice(0, 10)

async function load() {
  loading.value = true
  try {
    const [o, prods, custs, dels, invs, rets, accs] = await Promise.all([
      salesApi.get(id.value),
      masterData.products(),
      masterData.customers(),
      salesApi.deliveries(id.value),
      salesApi.invoices(id.value),
      salesApi.returns(id.value),
      accountingApi.accounts(),
    ])
    order.value = o
    products.value = nameMap(prods)
    customers.value = nameMap(custs)
    deliveries.value = dels
    invoices.value = invs
    returns.value = rets
    accounts.value = accs
  } finally {
    loading.value = false
  }
}

async function openReturn(inv: SalesInvoiceSummary) {
  returnError.value = ''
  refundAccountId.value = ''
  returnInvoiceId.value = inv.id
  returnInvoiceNo.value = inv.number
  const detail = await salesApi.getInvoice(inv.id)
  returnRows.value = detail.lines
    .filter((l) => l.returnableQuantity > 0)
    .map((l) => ({ salesInvoiceLineId: l.id, productId: l.productId, returnable: l.returnableQuantity, qty: 0 }))
  returnModal.value = true
}

async function submitReturn() {
  const lines = returnRows.value
    .filter((r) => r.qty > 0)
    .map((r) => ({ salesInvoiceLineId: r.salesInvoiceLineId, quantity: r.qty }))
  if (lines.length === 0) {
    returnError.value = t('sales.detail.returnNeedLine')
    return
  }
  returnSaving.value = true
  returnError.value = ''
  try {
    await salesApi.createReturn(returnInvoiceId.value, {
      returnDate: today(),
      notes: null,
      lines,
      refundCashAccountId: refundAccountId.value || null,
    })
    returnModal.value = false
    await load()
  } catch (e) {
    returnError.value =
      (e as { response?: { data?: { message?: string } } })?.response?.data?.message ??
      t('sales.detail.returnFailed')
  } finally {
    returnSaving.value = false
  }
}

const canDeliver = computed(
  () => order.value?.status === 'Approved' || order.value?.status === 'PartiallyDelivered',
)
const hasOutstanding = computed(() => order.value?.lines.some((l) => l.outstandingQuantity > 0) ?? false)
const hasUninvoiced = computed(
  () => order.value?.lines.some((l) => l.deliveredQuantity - l.invoicedQuantity > 0) ?? false,
)

const canEdit = computed(() => order.value?.status === 'Draft' && auth.has('Sales.Edit'))
const canCancel = computed(
  () =>
    (order.value?.status === 'Draft' || order.value?.status === 'PendingApproval') &&
    auth.has('Sales.Cancel'),
)

async function run(kind: 'submit' | 'deliver' | 'invoice' | 'cancel', fn: () => Promise<unknown>) {
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

function cancel() {
  if (!order.value || !window.confirm(t('sales.detail.confirmCancel'))) return
  run('cancel', () => salesApi.cancel(order.value!.id))
}

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
          <button
            class="inline-flex items-center gap-1.5 rounded-button border border-border px-3 py-2 text-sm font-medium text-text-muted transition-colors hover:bg-surface-2 hover:text-text"
            @click="downloadFile(`/sales-orders/${order.id}/quotation-pdf`, `quotation-${order.number}.pdf`)"
          >
            <FileText :size="16" /> {{ t('sales.detail.quotationPdf') }}
          </button>
          <AppButton
            v-if="canEdit"
            variant="secondary"
            :disabled="busy !== ''"
            @click="router.push({ name: 'salesOrderCreate', query: { edit: order.id } })"
          >
            <Pencil :size="16" /> {{ t('sales.detail.edit') }}
          </AppButton>
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
          <AppButton v-if="canCancel" variant="danger" :disabled="busy !== ''" @click="cancel">
            {{ busy === 'cancel' ? t('sales.detail.cancelling') : t('sales.detail.cancel') }}
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
            <thead>
              <tr class="border-b border-border text-xs uppercase tracking-wide text-text-muted">
                <th class="px-4 py-2 text-left font-semibold">{{ t('sales.detail.doNumber') }}</th>
                <th class="px-4 py-2 text-left font-semibold">{{ t('sales.detail.date') }}</th>
                <th class="px-4 py-2 text-right font-semibold">{{ t('sales.detail.cogs') }}</th>
                <th class="px-4 py-2"></th>
              </tr>
            </thead>
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
          <p v-if="deliveries.length" class="border-t border-border px-4 py-2 text-xs text-text-muted">
            {{ t('sales.detail.cogsHint') }}
          </p>
          <p v-else class="px-4 py-6 text-center text-sm text-text-muted">{{ t('sales.detail.noDeliveries') }}</p>
        </AppCard>

        <AppCard :title="t('sales.detail.invoices')" :padded="false">
          <table v-if="invoices.length" class="w-full text-sm">
            <tbody>
              <tr v-for="inv in invoices" :key="inv.id" class="border-b border-border last:border-0">
                <td class="px-4 py-2.5 text-text">{{ inv.number }}</td>
                <td class="px-4 py-2.5 text-text-muted">{{ t('sales.detail.due') }} {{ inv.dueDate }}</td>
                <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(inv.grandTotal, currency) }}</td>
                <td class="px-3 py-2.5 text-right">
                  <StatusBadge v-if="inv.journalEntryId" tone="positive" :label="t('sales.detail.posted')" />
                </td>
                <td class="px-3 py-2.5 text-right">
                  <div class="flex items-center justify-end gap-1">
                    <button
                      class="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium text-text-muted transition-colors hover:bg-surface-2 hover:text-text"
                      @click="downloadFile(`/sales-invoices/${inv.id}/pdf`, `invoice-${inv.number}.pdf`)"
                    >
                      <FileText :size="14" /> {{ t('sales.detail.pdf') }}
                    </button>
                    <button
                      v-if="inv.journalEntryId"
                      class="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium text-text-muted transition-colors hover:bg-surface-2 hover:text-text"
                      @click="openReturn(inv)"
                    >
                      <Undo2 :size="14" /> {{ t('sales.detail.return') }}
                    </button>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
          <p v-else class="px-4 py-6 text-center text-sm text-text-muted">{{ t('sales.detail.noInvoices') }}</p>
        </AppCard>
      </div>

      <!-- Returns (credit notes) -->
      <AppCard v-if="returns.length" :title="t('sales.detail.returns')" :padded="false">
        <table class="w-full text-sm">
          <tbody>
            <tr v-for="r in returns" :key="r.id" class="border-b border-border last:border-0">
              <td class="px-4 py-2.5 text-text">{{ r.number }}</td>
              <td class="px-4 py-2.5 text-text-muted">{{ r.returnDate }}</td>
              <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(r.grandTotal, currency) }}</td>
              <td class="px-4 py-2.5 text-right">
                <StatusBadge v-if="r.journalEntryId" tone="positive" :label="t('sales.detail.posted')" />
              </td>
            </tr>
          </tbody>
        </table>
      </AppCard>

      <AppCard v-if="order.notes" :title="t('sales.detail.notes')">
        <p class="text-sm text-text">{{ order.notes }}</p>
      </AppCard>

      <DocumentTimeline document-type="SalesOrder" :document-id="order.id" />
    </template>

    <AppModal v-model="returnModal" :title="`${t('sales.detail.returnTitle')} · ${returnInvoiceNo}`">
      <div class="space-y-3">
        <p v-if="returnRows.length === 0" class="text-sm text-text-muted">
          {{ t('sales.detail.returnNothing') }}
        </p>
        <table v-else class="w-full text-sm">
          <thead>
            <tr class="border-b border-border text-xs uppercase tracking-wide text-text-muted">
              <th class="py-2 text-left font-semibold">{{ t('sales.detail.product') }}</th>
              <th class="py-2 text-right font-semibold">{{ t('sales.detail.returnable') }}</th>
              <th class="py-2 text-right font-semibold">{{ t('sales.detail.returnQty') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="row in returnRows" :key="row.salesInvoiceLineId" class="border-b border-border last:border-0">
              <td class="py-2 text-text">{{ products.get(row.productId) ?? '—' }}</td>
              <td class="py-2 text-right text-text-muted tnum">{{ formatNumber(row.returnable) }}</td>
              <td class="py-2 text-right">
                <input
                  v-model.number="row.qty"
                  type="number" min="0" :max="row.returnable" step="any"
                  class="field-input w-28 text-right tnum"
                />
              </td>
            </tr>
          </tbody>
        </table>

        <FormField v-if="returnRows.length > 0" :label="t('sales.detail.refundAccount')">
          <AppSelect v-model="refundAccountId" :options="cashOptions" :placeholder="t('sales.detail.refundAccountNone')" />
          <p class="mt-1 text-xs text-text-muted">{{ t('sales.detail.refundHint') }}</p>
        </FormField>

        <p v-if="returnError" class="text-sm text-negative">{{ returnError }}</p>
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="returnModal = false">{{ t('masterData.cancel') }}</AppButton>
        <AppButton :disabled="returnSaving || returnRows.length === 0" @click="submitReturn">
          {{ returnSaving ? t('sales.detail.returning') : t('sales.detail.return') }}
        </AppButton>
      </template>
    </AppModal>
  </div>
</template>
