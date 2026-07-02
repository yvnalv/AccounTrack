<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { ArrowLeft, Plus, Trash2 } from 'lucide-vue-next'
import { expensesApi } from '@/lib/expenses'
import { masterData } from '@/lib/masterData'
import { accountingApi, cashAccounts } from '@/lib/accounting'
import { useCompanyStore } from '@/stores/company'
import { formatMoney } from '@/lib/format'
import type { CreateExpenseVoucher, ExpenseCategory } from '@/types/expenses'
import type { Supplier } from '@/types/masterdata'
import type { AccountRef } from '@/types/accounting'
import AppButton from '@/components/ui/AppButton.vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppInput from '@/components/ui/AppInput.vue'
import AppSelect from '@/components/ui/AppSelect.vue'
import FormField from '@/components/ui/FormField.vue'

interface LineForm {
  expenseCategoryId: string
  description: string
  amount: number
  taxed: boolean
}

const { t } = useI18n()
const router = useRouter()
const route = useRoute()
const company = useCompanyStore()

const editId = route.query.edit ? String(route.query.edit) : null

const today = new Date().toISOString().slice(0, 10)
const plus30 = new Date(Date.now() + 30 * 86_400_000).toISOString().slice(0, 10)

const form = reactive({
  expenseDate: today,
  payeeName: '',
  onAccount: false,
  cashAccountId: '',
  supplierId: '',
  dueDate: plus30,
  reference: '',
  notes: '',
})
const lines = reactive<LineForm[]>([{ expenseCategoryId: '', description: '', amount: 0, taxed: false }])

const categories = ref<ExpenseCategory[]>([])
const suppliers = ref<Supplier[]>([])
const accounts = ref<AccountRef[]>([])
const error = ref('')
const busy = ref<'' | 'draft' | 'post'>('')

const categoryOptions = computed(() =>
  categories.value.filter((c) => c.isActive).map((c) => ({ value: c.id, label: `${c.code} — ${c.name}` })),
)
const cashOptions = computed(() => cashAccounts(accounts.value).map((a) => ({ value: a.id, label: `${a.code} — ${a.name}` })))
const supplierOptions = computed(() =>
  suppliers.value.filter((s) => s.isActive).map((s) => ({ value: s.id, label: `${s.code} — ${s.name}` })),
)

const subTotal = computed(() => lines.reduce((s, l) => s + (Number(l.amount) || 0), 0))
const taxTotal = computed(() => lines.reduce((s, l) => s + (l.taxed ? (Number(l.amount) || 0) * 0.11 : 0), 0))
const grandTotal = computed(() => subTotal.value + taxTotal.value)

function addLine() {
  lines.push({ expenseCategoryId: categoryOptions.value[0]?.value ?? '', description: '', amount: 0, taxed: false })
}
function removeLine(i: number) {
  lines.splice(i, 1)
}

const validLines = computed(() => lines.filter((l) => l.expenseCategoryId && l.amount > 0))
const canSave = computed(
  () =>
    (form.onAccount ? !!form.supplierId && !!form.dueDate : !!form.cashAccountId) && validLines.value.length > 0,
)

onMounted(async () => {
  const [cats, sups, accs] = await Promise.all([
    expensesApi.categories(),
    masterData.suppliers(),
    accountingApi.accounts(),
    company.ensure(),
  ])
  categories.value = cats
  suppliers.value = sups
  accounts.value = accs

  if (editId) {
    const v = await expensesApi.getVoucher(editId)
    form.expenseDate = v.expenseDate
    form.payeeName = v.payeeName ?? ''
    form.onAccount = !!v.supplierId
    form.cashAccountId = v.cashAccountId ?? ''
    form.supplierId = v.supplierId ?? ''
    form.dueDate = v.dueDate ?? plus30
    form.reference = v.reference ?? ''
    form.notes = v.notes ?? ''
    lines.splice(
      0,
      lines.length,
      ...v.lines.map((l) => ({
        expenseCategoryId: l.expenseCategoryId,
        description: l.description ?? '',
        amount: l.amount,
        taxed: l.taxRate > 0,
      })),
    )
  } else {
    form.cashAccountId = cashOptions.value[0]?.value ?? ''
    form.supplierId = supplierOptions.value[0]?.value ?? ''
    lines[0].expenseCategoryId = categoryOptions.value[0]?.value ?? ''
  }
})

function payload(): CreateExpenseVoucher {
  return {
    expenseDate: form.expenseDate,
    payeeName: form.payeeName || null,
    cashAccountId: form.onAccount ? null : form.cashAccountId,
    supplierId: form.onAccount ? form.supplierId : null,
    dueDate: form.onAccount ? form.dueDate : null,
    reference: form.reference || null,
    notes: form.notes || null,
    lines: validLines.value.map((l) => ({
      expenseCategoryId: l.expenseCategoryId,
      description: l.description || null,
      amount: l.amount,
      taxRate: l.taxed ? 0.11 : 0,
    })),
  }
}

async function save(mode: 'draft' | 'post') {
  error.value = ''
  if (!canSave.value) {
    error.value = t('expenses.form.needLine')
    return
  }
  busy.value = mode
  try {
    const body = payload()
    let id: string
    if (editId) {
      await expensesApi.updateVoucher(editId, body)
      if (mode === 'post') await expensesApi.submitVoucher(editId)
      id = editId
    } else if (mode === 'post') {
      id = await expensesApi.createVoucher(body)
    } else {
      id = await expensesApi.createDraft(body)
    }
    await router.push({ name: 'expenseDetail', params: { id } })
  } catch (e) {
    error.value =
      (e as { response?: { status?: number } })?.response?.status === 409
        ? t('expenses.form.conflict')
        : t('expenses.form.failed')
  } finally {
    busy.value = ''
  }
}
</script>

<template>
  <div class="space-y-5">
    <button
      class="inline-flex items-center gap-1.5 text-sm text-text-muted hover:text-text"
      @click="router.push({ name: 'expenses' })"
    >
      <ArrowLeft :size="16" /> {{ t('expenses.backToList') }}
    </button>

    <h2 class="text-xl font-semibold text-text">{{ editId ? t('expenses.form.editTitle') : t('expenses.new') }}</h2>

    <AppCard>
      <div class="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <FormField :label="t('expenses.fields.date')" required><AppInput v-model="form.expenseDate" type="date" /></FormField>
        <FormField :label="t('expenses.fields.payee')"><AppInput v-model="form.payeeName" /></FormField>
        <FormField :label="t('expenses.fields.reference')"><AppInput v-model="form.reference" /></FormField>
      </div>

      <div class="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-3">
        <FormField :label="t('expenses.fields.paymentMode')">
          <div class="inline-flex rounded-md border border-border p-0.5">
            <button
              type="button"
              class="rounded px-3 py-1 text-sm font-medium transition-colors"
              :class="!form.onAccount ? 'bg-accent text-white' : 'text-text-muted hover:text-text'"
              @click="form.onAccount = false"
            >
              {{ t('expenses.fields.paid') }}
            </button>
            <button
              type="button"
              class="rounded px-3 py-1 text-sm font-medium transition-colors"
              :class="form.onAccount ? 'bg-accent text-white' : 'text-text-muted hover:text-text'"
              @click="form.onAccount = true"
            >
              {{ t('expenses.fields.onAccount') }}
            </button>
          </div>
        </FormField>

        <FormField v-if="!form.onAccount" :label="t('expenses.fields.paidFrom')" required>
          <AppSelect v-model="form.cashAccountId" :options="cashOptions" :placeholder="t('expenses.fields.selectAccount')" />
        </FormField>
        <template v-else>
          <FormField :label="t('expenses.fields.supplier')" required>
            <AppSelect v-model="form.supplierId" :options="supplierOptions" :placeholder="t('expenses.fields.selectSupplier')" />
          </FormField>
          <FormField :label="t('expenses.fields.dueDate')" required><AppInput v-model="form.dueDate" type="date" /></FormField>
        </template>
      </div>
    </AppCard>

    <AppCard :title="t('expenses.detail.lines')" :padded="false">
      <div class="overflow-x-auto">
        <table class="w-full text-sm">
          <thead>
            <tr class="border-b border-border text-xs uppercase tracking-wide text-text-muted">
              <th class="px-4 py-2.5 text-left font-semibold">{{ t('expenses.detail.category') }}</th>
              <th class="px-3 py-2.5 text-left font-semibold">{{ t('expenses.detail.description') }}</th>
              <th class="px-3 py-2.5 text-right font-semibold w-40">{{ t('expenses.detail.amount') }}</th>
              <th v-if="company.vatRegistered" class="px-3 py-2.5 text-center font-semibold w-24">{{ t('expenses.fields.ppn') }}</th>
              <th class="w-10"></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(line, i) in lines" :key="i" class="border-b border-border last:border-0">
              <td class="px-4 py-2">
                <select v-model="line.expenseCategoryId" class="field-input">
                  <option value="" disabled>{{ t('expenses.fields.selectCategory') }}</option>
                  <option v-for="o in categoryOptions" :key="o.value" :value="o.value">{{ o.label }}</option>
                </select>
              </td>
              <td class="px-3 py-2"><AppInput v-model="line.description" :placeholder="t('expenses.fields.description')" /></td>
              <td class="px-3 py-2">
                <input v-model.number="line.amount" type="number" min="0" step="any" class="field-input text-right tnum" />
              </td>
              <td v-if="company.vatRegistered" class="px-3 py-2 text-center">
                <input v-model="line.taxed" type="checkbox" class="h-4 w-4 accent-accent" />
              </td>
              <td class="px-2 py-2 text-center">
                <button
                  type="button"
                  class="grid h-8 w-8 place-items-center rounded-control text-text-muted hover:bg-surface-2 hover:text-negative"
                  :disabled="lines.length === 1"
                  @click="removeLine(i)"
                >
                  <Trash2 :size="16" />
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="flex items-center justify-between border-t border-border p-4">
        <AppButton variant="secondary" @click="addLine"><Plus :size="16" /> {{ t('expenses.fields.addLine') }}</AppButton>
        <dl class="w-full max-w-xs space-y-1.5 text-sm">
          <div class="flex justify-between">
            <dt class="text-text-muted">{{ t('expenses.detail.subtotal') }}</dt>
            <dd class="tnum text-text">{{ formatMoney(subTotal) }}</dd>
          </div>
          <div v-if="company.vatRegistered" class="flex justify-between">
            <dt class="text-text-muted">{{ t('expenses.detail.taxTotal') }}</dt>
            <dd class="tnum text-text">{{ formatMoney(taxTotal) }}</dd>
          </div>
          <div class="flex justify-between border-t border-border pt-1.5 font-semibold">
            <dt class="text-text">{{ t('expenses.detail.grandTotal') }}</dt>
            <dd class="tnum text-text">{{ formatMoney(grandTotal) }}</dd>
          </div>
        </dl>
      </div>
    </AppCard>

    <AppCard>
      <FormField :label="t('expenses.detail.notes')"><AppInput v-model="form.notes" /></FormField>
    </AppCard>

    <div class="flex items-center justify-end gap-3">
      <p v-if="error" class="text-sm text-negative">{{ error }}</p>
      <AppButton variant="secondary" :disabled="!canSave || busy !== ''" @click="save('draft')">
        {{ busy === 'draft' ? t('expenses.savingDraft') : (editId ? t('expenses.update') : t('expenses.saveDraft')) }}
      </AppButton>
      <AppButton :disabled="!canSave || busy !== ''" @click="save('post')">
        {{ busy === 'post' ? t('expenses.saving') : t('expenses.saveAndPost') }}
      </AppButton>
    </div>
  </div>
</template>
