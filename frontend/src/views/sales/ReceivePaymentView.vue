<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { ArrowLeft } from 'lucide-vue-next'
import { apiErrorMessage } from '@/lib/api'
import { salesApi } from '@/lib/sales'
import { masterData } from '@/lib/masterData'
import { accountingApi, cashAccounts } from '@/lib/accounting'
import { formatMoney } from '@/lib/format'
import type { NamedRef } from '@/types/masterdata'
import type { AccountRef, SubledgerOpenItem } from '@/types/accounting'
import AppButton from '@/components/ui/AppButton.vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppInput from '@/components/ui/AppInput.vue'
import AppSelect from '@/components/ui/AppSelect.vue'
import FormField from '@/components/ui/FormField.vue'

interface AllocRow {
  id: string
  documentNo: string
  dueDate: string
  currency: string
  outstanding: number
  amount: number
}

const { t } = useI18n()
const router = useRouter()
const route = useRoute()

const customers = ref<NamedRef[]>([])
const accounts = ref<AccountRef[]>([])
const form = ref({ customerId: '', cashAccountId: '', paymentDate: new Date().toISOString().slice(0, 10), reference: '' })
const rows = ref<AllocRow[]>([])
const loadingItems = ref(false)
const submitting = ref(false)
const error = ref('')
const success = ref('')

onMounted(async () => {
  const [c, a] = await Promise.all([masterData.customers(), accountingApi.accounts()])
  customers.value = c
  accounts.value = cashAccounts(a)
  // Preselect the customer when arriving from a customer detail page (watch loads their open items).
  const preselect = route.query.customerId
  if (typeof preselect === 'string' && c.some((x) => x.id === preselect)) {
    form.value.customerId = preselect
  }
})

const customerOptions = computed(() => customers.value.map((c) => ({ value: c.id, label: `${c.code} — ${c.name}` })))
const accountOptions = computed(() => accounts.value.map((a) => ({ value: a.id, label: `${a.code} — ${a.name}` })))

async function loadOpenItems(customerId: string) {
  rows.value = []
  if (!customerId) return
  loadingItems.value = true
  try {
    const items: SubledgerOpenItem[] = await accountingApi.arOpenItems(customerId)
    rows.value = items.map((i) => ({
      id: i.id,
      documentNo: i.documentNo,
      dueDate: i.dueDate,
      currency: i.currency,
      outstanding: i.outstandingAmount,
      amount: i.outstandingAmount, // default: pay in full; edit down to skip
    }))
  } finally {
    loadingItems.value = false
  }
}

watch(
  () => form.value.customerId,
  (id) => {
    error.value = ''
    success.value = ''
    void loadOpenItems(id)
  },
)

const currency = computed(() => rows.value[0]?.currency ?? 'IDR')
const total = computed(() => rows.value.reduce((s, r) => s + (r.amount > 0 ? r.amount : 0), 0))
const canSubmit = computed(
  () => !!form.value.customerId && !!form.value.cashAccountId && rows.value.some((r) => r.amount > 0),
)

async function submit() {
  error.value = ''
  success.value = ''
  if (!canSubmit.value) {
    error.value = t('sales.payment.needAlloc')
    return
  }
  submitting.value = true
  try {
    await salesApi.createCustomerPayment({
      customerId: form.value.customerId,
      cashAccountId: form.value.cashAccountId,
      paymentDate: form.value.paymentDate,
      reference: form.value.reference || null,
      notes: null,
      allocations: rows.value
        .filter((r) => r.amount > 0)
        .map((r) => ({ arOpenItemId: r.id, amount: r.amount })),
    })
    success.value = t('sales.payment.success')
    await loadOpenItems(form.value.customerId)
  } catch (e) {
    error.value = apiErrorMessage(e, t('sales.payment.failed'))
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="space-y-5">
    <button
      class="inline-flex items-center gap-1.5 text-sm text-text-muted hover:text-text"
      @click="router.push({ name: 'sales' })"
    >
      <ArrowLeft :size="16" /> {{ t('sales.backToList') }}
    </button>

    <AppCard>
      <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <FormField :label="t('sales.payment.customer')" required>
          <AppSelect v-model="form.customerId" :options="customerOptions" :placeholder="t('sales.payment.selectCustomer')" />
        </FormField>
        <FormField :label="t('sales.payment.cashAccount')" required>
          <AppSelect v-model="form.cashAccountId" :options="accountOptions" :placeholder="t('sales.payment.selectAccount')" />
        </FormField>
        <FormField :label="t('sales.payment.date')" required>
          <AppInput v-model="form.paymentDate" type="date" />
        </FormField>
        <FormField :label="t('sales.payment.reference')">
          <AppInput v-model="form.reference" :placeholder="t('sales.payment.reference')" />
        </FormField>
      </div>
    </AppCard>

    <AppCard v-if="form.customerId" :title="t('sales.payment.openItems')" :padded="false">
      <p v-if="loadingItems" class="px-4 py-6 text-center text-sm text-text-muted">{{ t('common.loading') }}</p>
      <p v-else-if="rows.length === 0" class="px-4 py-8 text-center text-sm text-text-muted">
        {{ t('sales.payment.noOpenItems') }}
      </p>
      <template v-else>
        <div class="overflow-x-auto">
          <table class="w-full text-sm">
            <thead>
              <tr class="border-b border-border text-xs uppercase tracking-wide text-text-muted">
                <th class="px-4 py-2.5 text-left font-semibold">{{ t('sales.payment.invoice') }}</th>
                <th class="px-4 py-2.5 text-left font-semibold">{{ t('sales.payment.due') }}</th>
                <th class="px-4 py-2.5 text-right font-semibold">{{ t('sales.payment.outstanding') }}</th>
                <th class="px-4 py-2.5 text-right font-semibold w-44">{{ t('sales.payment.amount') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in rows" :key="row.id" class="border-b border-border last:border-0">
                <td class="px-4 py-2.5 text-text">{{ row.documentNo }}</td>
                <td class="px-4 py-2.5 text-text-muted">{{ row.dueDate }}</td>
                <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(row.outstanding, row.currency) }}</td>
                <td class="px-4 py-2">
                  <input
                    v-model.number="row.amount"
                    type="number"
                    min="0"
                    :max="row.outstanding"
                    step="any"
                    class="field-input text-right tnum"
                  />
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <div class="flex items-center justify-between gap-4 border-t border-border p-4">
          <div class="text-sm">
            <span v-if="success" class="text-positive">{{ success }}</span>
            <span v-else-if="error" class="text-negative">{{ error }}</span>
          </div>
          <div class="flex items-center gap-4">
            <div class="text-sm">
              <span class="text-text-muted">{{ t('sales.payment.total') }}: </span>
              <span class="font-semibold text-text tnum">{{ formatMoney(total, currency) }}</span>
            </div>
            <AppButton :disabled="!canSubmit || submitting" @click="submit">
              {{ submitting ? t('sales.payment.saving') : t('sales.payment.submit') }}
            </AppButton>
          </div>
        </div>
      </template>
    </AppCard>
  </div>
</template>
