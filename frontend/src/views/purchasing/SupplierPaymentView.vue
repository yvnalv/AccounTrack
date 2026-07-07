<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { ArrowLeft } from 'lucide-vue-next'
import { purchasingApi } from '@/lib/purchasing'
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

const suppliers = ref<NamedRef[]>([])
const accounts = ref<AccountRef[]>([])
const form = ref({ supplierId: '', cashAccountId: '', paymentDate: new Date().toISOString().slice(0, 10), reference: '' })
const rows = ref<AllocRow[]>([])
const loadingItems = ref(false)
const submitting = ref(false)
const error = ref('')
const success = ref('')

onMounted(async () => {
  const [s, a] = await Promise.all([masterData.suppliers(), accountingApi.accounts()])
  suppliers.value = s
  accounts.value = cashAccounts(a)
  // Preselect the supplier when arriving from a supplier detail page (watch loads their open items).
  const preselect = route.query.supplierId
  if (typeof preselect === 'string' && s.some((x) => x.id === preselect)) {
    form.value.supplierId = preselect
  }
})

const supplierOptions = computed(() => suppliers.value.map((s) => ({ value: s.id, label: `${s.code} — ${s.name}` })))
const accountOptions = computed(() => accounts.value.map((a) => ({ value: a.id, label: `${a.code} — ${a.name}` })))

async function loadOpenItems(supplierId: string) {
  rows.value = []
  if (!supplierId) return
  loadingItems.value = true
  try {
    const items: SubledgerOpenItem[] = await accountingApi.apOpenItems(supplierId)
    rows.value = items.map((i) => ({
      id: i.id,
      documentNo: i.documentNo,
      dueDate: i.dueDate,
      currency: i.currency,
      outstanding: i.outstandingAmount,
      amount: i.outstandingAmount,
    }))
  } finally {
    loadingItems.value = false
  }
}

watch(
  () => form.value.supplierId,
  (id) => {
    error.value = ''
    success.value = ''
    void loadOpenItems(id)
  },
)

const currency = computed(() => rows.value[0]?.currency ?? 'IDR')
const total = computed(() => rows.value.reduce((s, r) => s + (r.amount > 0 ? r.amount : 0), 0))
const canSubmit = computed(
  () => !!form.value.supplierId && !!form.value.cashAccountId && rows.value.some((r) => r.amount > 0),
)

async function submit() {
  error.value = ''
  success.value = ''
  if (!canSubmit.value) {
    error.value = t('purchasing.payment.needAlloc')
    return
  }
  submitting.value = true
  try {
    await purchasingApi.createSupplierPayment({
      supplierId: form.value.supplierId,
      cashAccountId: form.value.cashAccountId,
      paymentDate: form.value.paymentDate,
      reference: form.value.reference || null,
      notes: null,
      allocations: rows.value.filter((r) => r.amount > 0).map((r) => ({ apOpenItemId: r.id, amount: r.amount })),
    })
    success.value = t('purchasing.payment.success')
    await loadOpenItems(form.value.supplierId)
  } catch {
    error.value = t('purchasing.payment.failed')
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="space-y-5">
    <button
      class="inline-flex items-center gap-1.5 text-sm text-text-muted hover:text-text"
      @click="router.push({ name: 'purchasing' })"
    >
      <ArrowLeft :size="16" /> {{ t('purchasing.backToList') }}
    </button>

    <AppCard>
      <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <FormField :label="t('purchasing.payment.supplier')" required>
          <AppSelect v-model="form.supplierId" :options="supplierOptions" :placeholder="t('purchasing.payment.selectSupplier')" />
        </FormField>
        <FormField :label="t('purchasing.payment.cashAccount')" required>
          <AppSelect v-model="form.cashAccountId" :options="accountOptions" :placeholder="t('purchasing.payment.selectAccount')" />
        </FormField>
        <FormField :label="t('purchasing.payment.date')" required>
          <AppInput v-model="form.paymentDate" type="date" />
        </FormField>
        <FormField :label="t('purchasing.payment.reference')">
          <AppInput v-model="form.reference" :placeholder="t('purchasing.payment.reference')" />
        </FormField>
      </div>
    </AppCard>

    <AppCard v-if="form.supplierId" :title="t('purchasing.payment.openItems')" :padded="false">
      <p v-if="loadingItems" class="px-4 py-6 text-center text-sm text-text-muted">{{ t('common.loading') }}</p>
      <p v-else-if="rows.length === 0" class="px-4 py-8 text-center text-sm text-text-muted">
        {{ t('purchasing.payment.noOpenItems') }}
      </p>
      <template v-else>
        <div class="overflow-x-auto">
          <table class="w-full text-sm">
            <thead>
              <tr class="border-b border-border text-xs uppercase tracking-wide text-text-muted">
                <th class="px-4 py-2.5 text-left font-semibold">{{ t('purchasing.payment.bill') }}</th>
                <th class="px-4 py-2.5 text-left font-semibold">{{ t('purchasing.payment.due') }}</th>
                <th class="px-4 py-2.5 text-right font-semibold">{{ t('purchasing.payment.outstanding') }}</th>
                <th class="px-4 py-2.5 text-right font-semibold w-44">{{ t('purchasing.payment.amount') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in rows" :key="row.id" class="border-b border-border last:border-0">
                <td class="px-4 py-2.5 text-text">{{ row.documentNo }}</td>
                <td class="px-4 py-2.5 text-text-muted">{{ row.dueDate }}</td>
                <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(row.outstanding, row.currency) }}</td>
                <td class="px-4 py-2">
                  <input v-model.number="row.amount" type="number" min="0" :max="row.outstanding" step="any" class="field-input text-right tnum" />
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
              <span class="text-text-muted">{{ t('purchasing.payment.total') }}: </span>
              <span class="font-semibold text-text tnum">{{ formatMoney(total, currency) }}</span>
            </div>
            <AppButton :disabled="!canSubmit || submitting" @click="submit">
              {{ submitting ? t('purchasing.payment.saving') : t('purchasing.payment.submit') }}
            </AppButton>
          </div>
        </div>
      </template>
    </AppCard>
  </div>
</template>
