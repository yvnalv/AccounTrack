<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { Plus, Tags, Trash2 } from 'lucide-vue-next'
import { expensesApi } from '@/lib/expenses'
import { masterData } from '@/lib/masterData'
import { accountingApi, cashAccounts } from '@/lib/accounting'
import { useCompanyStore } from '@/stores/company'
import { downloadExport } from '@/lib/api'
import { formatMoney, formatMoneyShort } from '@/lib/format'
import type { ExpenseCategory, ExpenseVoucherSummary } from '@/types/expenses'
import type { Supplier } from '@/types/masterdata'
import type { AccountRef } from '@/types/accounting'
import AppButton from '@/components/ui/AppButton.vue'
import AppInput from '@/components/ui/AppInput.vue'
import AppModal from '@/components/ui/AppModal.vue'
import AppSelect from '@/components/ui/AppSelect.vue'
import DataTable from '@/components/ui/DataTable.vue'
import ExportMenu from '@/components/ui/ExportMenu.vue'
import FormField from '@/components/ui/FormField.vue'
import InsightCards, { type Insight } from '@/components/ui/InsightCards.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import type { Column } from '@/components/ui/types'

const { t } = useI18n()
const company = useCompanyStore()

const rows = ref<ExpenseVoucherSummary[]>([])

const insights = computed<Insight[]>(() => {
  const total = rows.value.reduce((s, v) => s + v.grandTotal, 0)
  return [
    { label: t('common.insights.vouchers'), value: String(rows.value.length) },
    { label: t('common.insights.value'), value: formatMoneyShort(total), tone: 'accent' },
  ]
})
const categories = ref<ExpenseCategory[]>([])
const accounts = ref<AccountRef[]>([])
const suppliers = ref<Supplier[]>([])
const loading = ref(true)
const modalOpen = ref(false)
const saving = ref(false)
const error = ref('')

const today = () => new Date().toISOString().slice(0, 10)
const plus30 = () => {
  const d = new Date()
  d.setDate(d.getDate() + 30)
  return d.toISOString().slice(0, 10)
}

interface LineForm {
  expenseCategoryId: string
  description: string
  amount: number
  taxed: boolean
}
const form = reactive({
  expenseDate: today(),
  payeeName: '',
  onAccount: false,
  cashAccountId: '',
  supplierId: '',
  dueDate: plus30(),
  reference: '',
  lines: [] as LineForm[],
})

const columns = computed<Column[]>(() => [
  { key: 'number', label: t('expenses.columns.number') },
  { key: 'expenseDate', label: t('expenses.columns.date') },
  { key: 'payeeName', label: t('expenses.columns.payee') },
  { key: 'grandTotal', label: t('expenses.columns.total'), align: 'right', numeric: true },
  { key: 'journalEntryId', label: t('expenses.columns.status'), align: 'right' },
])

const categoryOptions = computed(() =>
  categories.value.filter((c) => c.isActive).map((c) => ({ value: c.id, label: `${c.code} — ${c.name}` })),
)
const cashOptions = computed(() =>
  cashAccounts(accounts.value).map((a) => ({ value: a.id, label: `${a.code} — ${a.name}` })),
)
const supplierOptions = computed(() =>
  suppliers.value.filter((s) => s.isActive).map((s) => ({ value: s.id, label: `${s.code} — ${s.name}` })),
)
const grandTotal = computed(() =>
  form.lines.reduce((sum, l) => sum + (Number(l.amount) || 0) * (l.taxed ? 1.11 : 1), 0),
)

async function load() {
  loading.value = true
  try {
    const [vs, cats, accs, sups] = await Promise.all([
      expensesApi.vouchers(),
      expensesApi.categories(),
      accountingApi.accounts(),
      masterData.suppliers(),
      company.ensure(),
    ])
    rows.value = vs
    categories.value = cats
    accounts.value = accs
    suppliers.value = sups
  } finally {
    loading.value = false
  }
}
onMounted(load)

function addLine() {
  form.lines.push({ expenseCategoryId: categoryOptions.value[0]?.value ?? '', description: '', amount: 0, taxed: false })
}

function removeLine(i: number) {
  form.lines.splice(i, 1)
}

function openNew() {
  error.value = ''
  Object.assign(form, {
    expenseDate: today(),
    payeeName: '',
    onAccount: false,
    cashAccountId: cashOptions.value[0]?.value ?? '',
    supplierId: supplierOptions.value[0]?.value ?? '',
    dueDate: plus30(),
    reference: '',
    lines: [],
  })
  addLine()
  modalOpen.value = true
}

const canSave = computed(
  () =>
    (form.onAccount ? !!form.supplierId && !!form.dueDate : !!form.cashAccountId) &&
    form.lines.length > 0 &&
    form.lines.every((l) => l.expenseCategoryId && l.amount > 0),
)

// --- Category management ---
const catModalOpen = ref(false)
const catSaving = ref(false)
const catError = ref('')
const catForm = reactive({ id: '', code: '', name: '', postingRuleKey: '' })
const editingCat = computed(() => !!catForm.id)

function resetCatForm() {
  Object.assign(catForm, { id: '', code: '', name: '', postingRuleKey: '' })
  catError.value = ''
}

function openCategories() {
  resetCatForm()
  catModalOpen.value = true
}

function editCat(c: ExpenseCategory) {
  Object.assign(catForm, { id: c.id, code: c.code, name: c.name, postingRuleKey: c.postingRuleKey })
  catError.value = ''
}

const canSaveCat = computed(() =>
  editingCat.value
    ? !!catForm.name && !!catForm.postingRuleKey
    : !!catForm.code && !!catForm.name && !!catForm.postingRuleKey,
)

async function saveCat() {
  catError.value = ''
  catSaving.value = true
  try {
    if (editingCat.value) {
      await expensesApi.updateCategory(catForm.id, { name: catForm.name, postingRuleKey: catForm.postingRuleKey })
    } else {
      await expensesApi.createCategory({ code: catForm.code, name: catForm.name, postingRuleKey: catForm.postingRuleKey })
    }
    categories.value = await expensesApi.categories()
    resetCatForm()
  } catch {
    catError.value = t('expenses.categories.saveFailed')
  } finally {
    catSaving.value = false
  }
}

async function toggleCat(c: ExpenseCategory) {
  catError.value = ''
  try {
    await expensesApi.setCategoryActive(c.id, !c.isActive)
    categories.value = await expensesApi.categories()
  } catch {
    catError.value = t('expenses.categories.saveFailed')
  }
}

async function save() {
  error.value = ''
  saving.value = true
  try {
    await expensesApi.createVoucher({
      expenseDate: form.expenseDate,
      payeeName: form.payeeName || null,
      cashAccountId: form.onAccount ? null : form.cashAccountId,
      supplierId: form.onAccount ? form.supplierId : null,
      dueDate: form.onAccount ? form.dueDate : null,
      reference: form.reference || null,
      notes: null,
      lines: form.lines.map((l) => ({
        expenseCategoryId: l.expenseCategoryId,
        description: l.description || null,
        amount: l.amount,
        taxRate: l.taxed ? 0.11 : 0,
      })),
    })
    modalOpen.value = false
    await load()
  } catch {
    error.value = t('expenses.failed')
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="space-y-4">
    <InsightCards :items="insights" />
    <div class="flex justify-end gap-2">
      <ExportMenu :download="(f) => downloadExport('/expense-vouchers/export', 'expense-vouchers', f)" />
      <AppButton variant="ghost" @click="openCategories"><Tags :size="16" /> {{ t('expenses.categories.manage') }}</AppButton>
      <AppButton @click="openNew"><Plus :size="16" /> {{ t('expenses.new') }}</AppButton>
    </div>

    <DataTable searchable :columns="columns" :rows="rows" :loading="loading" :empty-text="t('expenses.empty')">
      <template #cell-payeeName="{ value }">{{ value || '—' }}</template>
      <template #cell-grandTotal="{ value }">{{ formatMoney(Number(value)) }}</template>
      <template #cell-journalEntryId="{ value, row }">
        <div class="flex items-center justify-end gap-1.5">
          <StatusBadge v-if="row.supplierId" tone="warning" :label="t('expenses.unpaid')" />
          <StatusBadge v-if="value" tone="positive" :label="t('expenses.posted')" />
        </div>
      </template>
    </DataTable>

    <AppModal v-model="modalOpen" :title="t('expenses.new')">
      <div class="space-y-4">
        <div class="grid grid-cols-2 gap-4">
          <FormField :label="t('expenses.fields.date')" required><AppInput v-model="form.expenseDate" type="date" /></FormField>
          <FormField :label="t('expenses.fields.payee')"><AppInput v-model="form.payeeName" /></FormField>
        </div>
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

        <div class="grid grid-cols-2 gap-4">
          <FormField v-if="!form.onAccount" :label="t('expenses.fields.paidFrom')" required>
            <AppSelect v-model="form.cashAccountId" :options="cashOptions" :placeholder="t('expenses.fields.selectAccount')" />
          </FormField>
          <template v-else>
            <FormField :label="t('expenses.fields.supplier')" required>
              <AppSelect v-model="form.supplierId" :options="supplierOptions" :placeholder="t('expenses.fields.selectSupplier')" />
            </FormField>
            <FormField :label="t('expenses.fields.dueDate')" required>
              <AppInput v-model="form.dueDate" type="date" />
            </FormField>
          </template>
          <FormField :label="t('expenses.fields.reference')"><AppInput v-model="form.reference" /></FormField>
        </div>

        <div>
          <div class="mb-1.5 flex items-center justify-between">
            <span class="text-sm font-medium text-text">{{ t('expenses.fields.lines') }}</span>
            <button class="text-xs font-medium text-accent hover:underline" @click="addLine">+ {{ t('expenses.fields.addLine') }}</button>
          </div>
          <div class="space-y-2">
            <div v-for="(line, i) in form.lines" :key="i" class="flex items-end gap-2">
              <div class="flex-1">
                <AppSelect v-model="line.expenseCategoryId" :options="categoryOptions" :placeholder="t('expenses.fields.selectCategory')" />
              </div>
              <div class="flex-1">
                <AppInput v-model="line.description" :placeholder="t('expenses.fields.description')" />
              </div>
              <div class="w-32">
                <input v-model.number="line.amount" type="number" min="0" step="any" class="field-input text-right tnum" :placeholder="t('expenses.fields.amount')" />
              </div>
              <label v-if="company.vatRegistered" class="flex h-9 items-center gap-1.5 whitespace-nowrap text-xs text-text-muted">
                <input v-model="line.taxed" type="checkbox" class="h-4 w-4 accent-accent" /> {{ t('expenses.fields.ppn') }}
              </label>
              <button class="flex h-9 w-9 items-center justify-center rounded-md text-text-muted hover:bg-surface-2 hover:text-negative" @click="removeLine(i)">
                <Trash2 :size="15" />
              </button>
            </div>
          </div>
        </div>

        <div class="flex justify-end border-t border-border pt-3">
          <div class="flex w-56 justify-between text-sm font-semibold">
            <span class="text-text">{{ t('expenses.fields.total') }}</span>
            <span class="tnum text-text">{{ formatMoney(grandTotal) }}</span>
          </div>
        </div>

        <p v-if="error" class="text-sm text-negative">{{ error }}</p>
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="modalOpen = false">{{ t('masterData.cancel') }}</AppButton>
        <AppButton :disabled="saving || !canSave" @click="save">
          {{ saving ? t('expenses.saving') : t('expenses.post') }}
        </AppButton>
      </template>
    </AppModal>

    <AppModal v-model="catModalOpen" :title="t('expenses.categories.title')">
      <div class="space-y-4">
        <div class="rounded-lg border border-border p-3">
          <div class="grid grid-cols-3 gap-2">
            <FormField :label="t('expenses.categories.code')">
              <AppInput v-model="catForm.code" :disabled="editingCat" />
            </FormField>
            <FormField :label="t('expenses.categories.name')"><AppInput v-model="catForm.name" /></FormField>
            <FormField :label="t('expenses.categories.rule')"><AppInput v-model="catForm.postingRuleKey" /></FormField>
          </div>
          <div class="mt-2 flex justify-end gap-2">
            <AppButton v-if="editingCat" variant="ghost" @click="resetCatForm">{{ t('masterData.cancel') }}</AppButton>
            <AppButton :disabled="catSaving || !canSaveCat" @click="saveCat">
              {{ editingCat ? t('masterData.save') : t('expenses.categories.new') }}
            </AppButton>
          </div>
        </div>

        <p v-if="catError" class="text-sm text-negative">{{ catError }}</p>

        <div class="overflow-hidden rounded-lg border border-border">
          <table class="w-full text-sm">
            <thead class="bg-surface-2 text-left text-xs text-text-muted">
              <tr>
                <th class="px-3 py-2 font-medium">{{ t('expenses.categories.code') }}</th>
                <th class="px-3 py-2 font-medium">{{ t('expenses.categories.name') }}</th>
                <th class="px-3 py-2 font-medium">{{ t('expenses.categories.status') }}</th>
                <th class="px-3 py-2"></th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="!categories.length"><td colspan="4" class="px-3 py-4 text-center text-text-muted">{{ t('expenses.categories.empty') }}</td></tr>
              <tr v-for="c in categories" :key="c.id" class="border-t border-border" :class="{ 'opacity-50': !c.isActive }">
                <td class="px-3 py-2 font-mono text-xs">{{ c.code }}</td>
                <td class="px-3 py-2 text-text">{{ c.name }}</td>
                <td class="px-3 py-2">
                  <StatusBadge
                    :tone="c.isActive ? 'positive' : 'neutral'"
                    :label="c.isActive ? t('expenses.categories.active') : t('expenses.categories.inactive')"
                  />
                </td>
                <td class="px-3 py-2 text-right">
                  <button class="text-xs font-medium text-accent hover:underline" @click="editCat(c)">{{ t('masterData.edit') }}</button>
                  <button class="ml-3 text-xs font-medium text-text-muted hover:underline" @click="toggleCat(c)">
                    {{ c.isActive ? t('expenses.categories.deactivate') : t('expenses.categories.activate') }}
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="catModalOpen = false">{{ t('masterData.close') }}</AppButton>
      </template>
    </AppModal>
  </div>
</template>
