<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { Plus, Trash2 } from 'lucide-vue-next'
import { expensesApi } from '@/lib/expenses'
import { accountingApi, cashAccounts } from '@/lib/accounting'
import { formatMoney } from '@/lib/format'
import type { ExpenseCategory, ExpenseVoucherSummary } from '@/types/expenses'
import type { AccountRef } from '@/types/accounting'
import AppButton from '@/components/ui/AppButton.vue'
import AppInput from '@/components/ui/AppInput.vue'
import AppModal from '@/components/ui/AppModal.vue'
import AppSelect from '@/components/ui/AppSelect.vue'
import DataTable from '@/components/ui/DataTable.vue'
import FormField from '@/components/ui/FormField.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import type { Column } from '@/components/ui/types'

const { t } = useI18n()

const rows = ref<ExpenseVoucherSummary[]>([])
const categories = ref<ExpenseCategory[]>([])
const accounts = ref<AccountRef[]>([])
const loading = ref(true)
const modalOpen = ref(false)
const saving = ref(false)
const error = ref('')

const today = () => new Date().toISOString().slice(0, 10)

interface LineForm {
  expenseCategoryId: string
  description: string
  amount: number
  taxed: boolean
}
const form = reactive({
  expenseDate: today(),
  payeeName: '',
  cashAccountId: '',
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
const grandTotal = computed(() =>
  form.lines.reduce((sum, l) => sum + (Number(l.amount) || 0) * (l.taxed ? 1.11 : 1), 0),
)

async function load() {
  loading.value = true
  try {
    const [vs, cats, accs] = await Promise.all([
      expensesApi.vouchers(),
      expensesApi.categories(),
      accountingApi.accounts(),
    ])
    rows.value = vs
    categories.value = cats
    accounts.value = accs
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
    cashAccountId: cashOptions.value[0]?.value ?? '',
    reference: '',
    lines: [],
  })
  addLine()
  modalOpen.value = true
}

const canSave = computed(
  () => form.cashAccountId && form.lines.length > 0 && form.lines.every((l) => l.expenseCategoryId && l.amount > 0),
)

async function save() {
  error.value = ''
  saving.value = true
  try {
    await expensesApi.createVoucher({
      expenseDate: form.expenseDate,
      payeeName: form.payeeName || null,
      cashAccountId: form.cashAccountId,
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
    <div class="flex justify-end">
      <AppButton @click="openNew"><Plus :size="16" /> {{ t('expenses.new') }}</AppButton>
    </div>

    <DataTable :columns="columns" :rows="rows" :loading="loading" :empty-text="t('expenses.empty')">
      <template #cell-payeeName="{ value }">{{ value || '—' }}</template>
      <template #cell-grandTotal="{ value }">{{ formatMoney(Number(value)) }}</template>
      <template #cell-journalEntryId="{ value }">
        <StatusBadge v-if="value" tone="positive" :label="t('expenses.posted')" />
      </template>
    </DataTable>

    <AppModal v-model="modalOpen" :title="t('expenses.new')">
      <div class="space-y-4">
        <div class="grid grid-cols-2 gap-4">
          <FormField :label="t('expenses.fields.date')" required><AppInput v-model="form.expenseDate" type="date" /></FormField>
          <FormField :label="t('expenses.fields.payee')"><AppInput v-model="form.payeeName" /></FormField>
        </div>
        <div class="grid grid-cols-2 gap-4">
          <FormField :label="t('expenses.fields.paidFrom')" required>
            <AppSelect v-model="form.cashAccountId" :options="cashOptions" :placeholder="t('expenses.fields.selectAccount')" />
          </FormField>
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
              <label class="flex h-9 items-center gap-1.5 whitespace-nowrap text-xs text-text-muted">
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
  </div>
</template>
