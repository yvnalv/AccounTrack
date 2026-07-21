<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { Plus, Tags } from 'lucide-vue-next'
import { apiErrorMessage } from '@/lib/api'
import { expensesApi } from '@/lib/expenses'
import { exportTable } from '@/lib/exportTable'
import { formatMoney, formatMoneyShort } from '@/lib/format'
import type { ExpenseCategory, ExpenseVoucherSummary } from '@/types/expenses'
import AppButton from '@/components/ui/AppButton.vue'
import AppInput from '@/components/ui/AppInput.vue'
import AppModal from '@/components/ui/AppModal.vue'
import DataTable from '@/components/ui/DataTable.vue'
import ExportMenu from '@/components/ui/ExportMenu.vue'
import FormField from '@/components/ui/FormField.vue'
import InsightCards, { type Insight } from '@/components/ui/InsightCards.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import type { Column } from '@/components/ui/types'
import { useAuthStore } from '@/stores/auth'

const { t } = useI18n()
const router = useRouter()
const auth = useAuthStore()

const vouchers = ref<ExpenseVoucherSummary[]>([])
const filteredRows = ref<Record<string, unknown>[]>([])
const categories = ref<ExpenseCategory[]>([])
const loading = ref(true)
const statusFilter = ref('')
const categoryFilter = ref('')
const statuses = ['Draft', 'PendingApproval', 'Posted', 'Rejected', 'Reversed', 'Cancelled']
const categoryOptions = computed(() =>
  categories.value.filter((c) => c.isActive).map((c) => ({ id: c.id, name: c.name })),
)

const rows = computed(() =>
  vouchers.value
    .filter((v) => !statusFilter.value || v.status === statusFilter.value)
    .filter((v) => !categoryFilter.value || v.categoryIds.includes(categoryFilter.value)),
)

const insights = computed<Insight[]>(() => {
  const total = vouchers.value.reduce((s, v) => s + v.grandTotal, 0)
  return [
    { label: t('common.insights.vouchers'), value: String(vouchers.value.length) },
    { label: t('common.insights.value'), value: formatMoneyShort(total), tone: 'accent' },
  ]
})

const columns = computed<Column[]>(() => [
  { key: 'number', label: t('expenses.columns.number') },
  { key: 'expenseDate', label: t('expenses.columns.date'), hideOnMobile: true },
  { key: 'categoryNames', label: t('expenses.columns.category'), hideOnMobile: true },
  { key: 'payeeName', label: t('expenses.columns.payee'), hideOnMobile: true },
  { key: 'grandTotal', label: t('expenses.columns.total'), align: 'right', numeric: true },
  { key: 'status', label: t('expenses.columns.status'), align: 'right' },
])

async function load() {
  loading.value = true
  try {
    const [vs, cats] = await Promise.all([expensesApi.vouchers(), expensesApi.categories()])
    vouchers.value = vs
    categories.value = cats
  } finally {
    loading.value = false
  }
}
onMounted(load)

function openVoucher(row: Record<string, unknown>) {
  router.push({ name: 'expenseDetail', params: { id: String(row.id) } })
}

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
  } catch (e) {
    catError.value = apiErrorMessage(e, t('expenses.categories.saveFailed'))
  } finally {
    catSaving.value = false
  }
}
async function toggleCat(c: ExpenseCategory) {
  catError.value = ''
  try {
    await expensesApi.setCategoryActive(c.id, !c.isActive)
    categories.value = await expensesApi.categories()
  } catch (e) {
    catError.value = apiErrorMessage(e, t('expenses.categories.saveFailed'))
  }
}
</script>

<template>
  <div class="space-y-4">
    <InsightCards :items="insights" />
    <div class="flex justify-end gap-2">
      <ExportMenu :download="(f) => exportTable(columns, filteredRows, 'expense-vouchers', f)" />
      <AppButton v-if="auth.has('Expenses.Manage')" variant="ghost" @click="openCategories"><Tags :size="16" /> {{ t('expenses.categories.manage') }}</AppButton>
      <AppButton v-if="auth.has('Expenses.Create')" @click="router.push({ name: 'expenseCreate' })"><Plus :size="16" /> {{ t('expenses.new') }}</AppButton>
    </div>

    <DataTable
      v-model:filtered="filteredRows"
      searchable
      :columns="columns"
      :rows="rows"
      :loading="loading"
      :empty-text="t('expenses.empty')"
      clickable
      :filters-active="!!statusFilter || !!categoryFilter"
      @row-click="openVoucher"
      @clear="statusFilter = ''; categoryFilter = ''"
    >
      <template #filters>
        <select v-model="statusFilter" class="field-input h-9 text-sm">
          <option value="">{{ t('common.allStatuses') }}</option>
          <option v-for="s in statuses" :key="s" :value="s">{{ t(`expenses.statusLabel.${s}`) }}</option>
        </select>
        <select v-model="categoryFilter" class="field-input h-9 text-sm">
          <option value="">{{ t('expenses.allCategories') }}</option>
          <option v-for="c in categoryOptions" :key="c.id" :value="c.id">{{ c.name }}</option>
        </select>
      </template>
      <template #cell-categoryNames="{ value }">{{ value || '—' }}</template>
      <template #cell-payeeName="{ value }">{{ value || '—' }}</template>
      <template #cell-grandTotal="{ value }">{{ formatMoney(Number(value)) }}</template>
      <template #cell-status="{ value, row }">
        <div class="flex items-center justify-end gap-1.5">
          <StatusBadge v-if="row.supplierId && value === 'Posted'" tone="warning" :label="t('expenses.unpaid')" />
          <StatusBadge :status="String(value)" :label="t(`expenses.statusLabel.${value}`)" />
        </div>
      </template>
    </DataTable>

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
