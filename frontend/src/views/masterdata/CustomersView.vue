<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { Plus, Upload, FileDown } from 'lucide-vue-next'
import { masterData } from '@/lib/masterData'
import { pricingApi } from '@/lib/pricing'
import { accountingApi } from '@/lib/accounting'
import { isConflict } from '@/lib/api'
import { exportTable } from '@/lib/exportTable'
import { formatMoney, formatMoneyShort } from '@/lib/format'
import { useCsvImport } from '@/composables/useCsvImport'
import type { Customer, PriceList } from '@/types/masterdata'
import AppButton from '@/components/ui/AppButton.vue'
import AppInput from '@/components/ui/AppInput.vue'
import AppModal from '@/components/ui/AppModal.vue'
import AppSelect from '@/components/ui/AppSelect.vue'
import CsvImportModal from '@/components/ui/CsvImportModal.vue'
import ExportMenu from '@/components/ui/ExportMenu.vue'
import DataTable from '@/components/ui/DataTable.vue'
import FormField from '@/components/ui/FormField.vue'
import InsightCards, { type Insight } from '@/components/ui/InsightCards.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import RowActions from '@/components/ui/RowActions.vue'
import type { Column } from '@/components/ui/types'
import { useAuthStore } from '@/stores/auth'

const { t } = useI18n()
const router = useRouter()
const auth = useAuthStore()
const rows = ref<Customer[]>([])
const filteredRows = ref<Record<string, unknown>[]>([])

function openDetail(row: Record<string, unknown>) {
  router.push({ name: 'masterDataCustomerDetail', params: { id: String(row.id) } })
}

// AR outstanding per customer (what they owe us), from the AR subledger aging.
const receivableByCustomer = ref(new Map<string, number>())
const overdueByCustomer = ref(new Map<string, number>())
const receivableTotal = ref(0)
const overdueTotal = ref(0)

const insights = computed<Insight[]>(() => {
  const total = rows.value.length
  const active = rows.value.filter((r) => r.isActive).length
  return [
    { label: t('masterData.tabs.customers'), value: String(total) },
    { label: t('common.insights.active'), value: String(active), tone: 'positive' },
    { label: t('masterData.customers.receivable'), value: formatMoneyShort(receivableTotal.value), tone: 'accent' },
    { label: t('masterData.customers.overdue'), value: formatMoneyShort(overdueTotal.value), tone: overdueTotal.value > 0 ? 'negative' : 'neutral' },
  ]
})

const tableRows = computed(() =>
  rows.value.map((c) => ({
    ...c,
    receivable: receivableByCustomer.value.get(c.id) ?? 0,
    overdue: overdueByCustomer.value.get(c.id) ?? 0,
  })),
)
const loading = ref(true)
const modalOpen = ref(false)
const saving = ref(false)
const error = ref('')
const editingId = ref<string | null>(null)
const editRowVersion = ref<string | null>(null)
const form = reactive({ code: '', name: '', taxId: '', paymentTermDays: 30, creditLimit: 0, salesPriceListId: '' })

// Sales price lists for the per-customer override (ADR-0035).
const salesLists = ref<PriceList[]>([])
const priceListOptions = computed(() => [
  { value: '', label: t('priceLists.assign.none') },
  ...salesLists.value.filter((l) => l.isActive).map((l) => ({ value: l.id, label: l.name })),
])

// CSV import/export (ADR-0031) via the shared composable + modal.
const io = masterData.customerImport
const { fileInput, open: importOpen, preview: importPreview, busy: importBusy, error: importError, canCommit, pick, onFileChosen, commit: commitImport } =
  useCsvImport(io, () => load())

const columns = computed<Column[]>(() => [
  { key: 'code', label: t('masterData.fields.code') },
  { key: 'name', label: t('masterData.fields.name') },
  { key: 'taxId', label: t('masterData.fields.taxId'), hideOnMobile: true },
  { key: 'paymentTermDays', label: t('masterData.fields.terms'), align: 'right', numeric: true, hideOnMobile: true },
  { key: 'creditLimit', label: t('masterData.fields.creditLimit'), align: 'right', numeric: true, hideOnMobile: true },
  { key: 'receivable', label: t('masterData.customers.receivable'), align: 'right', numeric: true },
  { key: 'isActive', label: t('masterData.status') },
  { key: 'actions', label: t('masterData.actions'), align: 'right' },
])

async function load() {
  loading.value = true
  try {
    const [c, lists] = await Promise.all([masterData.customers(), pricingApi.list()])
    rows.value = c
    salesLists.value = lists.filter((l) => l.type === 'Sales')

    // AR is cross-module (Accounting.View); degrade gracefully if the user lacks it.
    const aging = await accountingApi.arAging().catch(() => null)
    const receivable = new Map<string, number>()
    const overdue = new Map<string, number>()
    let rTotal = 0
    let oTotal = 0
    for (const r of aging?.rows ?? []) {
      const od = r.days1To30 + r.days31To60 + r.days61To90 + r.days90Plus
      receivable.set(r.partyId, r.total)
      overdue.set(r.partyId, od)
      rTotal += r.total
      oTotal += od
    }
    receivableByCustomer.value = receivable
    overdueByCustomer.value = overdue
    receivableTotal.value = rTotal
    overdueTotal.value = oTotal
  } finally {
    loading.value = false
  }
}
onMounted(load)

function openNew() {
  editingId.value = null
  Object.assign(form, { code: '', name: '', taxId: '', paymentTermDays: 30, creditLimit: 0, salesPriceListId: '' })
  error.value = ''
  modalOpen.value = true
}

function openEdit(row: Customer) {
  editingId.value = row.id
  editRowVersion.value = row.rowVersion
  Object.assign(form, {
    code: row.code,
    name: row.name,
    taxId: row.taxId ?? '',
    paymentTermDays: row.paymentTermDays,
    creditLimit: row.creditLimit,
    salesPriceListId: row.salesPriceListId ?? '',
  })
  error.value = ''
  modalOpen.value = true
}

async function save() {
  error.value = ''
  saving.value = true
  try {
    if (editingId.value) {
      await masterData.updateCustomer(editingId.value, {
        name: form.name,
        taxId: form.taxId || null,
        paymentTermDays: form.paymentTermDays,
        creditLimit: form.creditLimit,
        salesPriceListId: form.salesPriceListId || null,
      }, editRowVersion.value)
    } else {
      await masterData.createCustomer({
        code: form.code,
        name: form.name,
        taxId: form.taxId || null,
        paymentTermDays: form.paymentTermDays,
        creditLimit: form.creditLimit,
      })
    }
    modalOpen.value = false
    await load()
  } catch (e) {
    error.value = isConflict(e) ? t('masterData.conflict') : t('masterData.failed')
  } finally {
    saving.value = false
  }
}

async function toggleActive(row: Customer) {
  await masterData.setCustomerActive(row.id, !row.isActive)
  await load()
}
</script>

<template>
  <div class="space-y-4">
    <InsightCards :items="insights" />
    <div class="flex flex-wrap items-center justify-end gap-2">
      <input ref="fileInput" type="file" accept=".csv,.xlsx,text/csv,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" class="hidden" @change="onFileChosen" />
      <button class="inline-flex items-center gap-1.5 rounded-button border border-border px-3 py-2 text-sm font-medium text-text-muted transition-colors hover:text-text hover:bg-surface-2" @click="io.template()">
        <FileDown :size="16" /> {{ t('masterData.import.template') }}
      </button>
      <ExportMenu :download="(f) => exportTable(columns, filteredRows, 'customers', f)" />
      <button class="inline-flex items-center gap-1.5 rounded-button border border-border px-3 py-2 text-sm font-medium text-text-muted transition-colors hover:text-text hover:bg-surface-2" @click="pick">
        <Upload :size="16" /> {{ t('masterData.import.import') }}
      </button>
      <AppButton v-if="auth.has('MasterData.Create')" @click="openNew"><Plus :size="16" /> {{ t('masterData.customers.new') }}</AppButton>
    </div>

    <DataTable v-model:filtered="filteredRows" searchable clickable :columns="columns" :rows="tableRows" :loading="loading" :empty-text="t('masterData.empty')" @row-click="openDetail">
      <template #cell-taxId="{ value }">{{ value || '—' }}</template>
      <template #cell-creditLimit="{ value }">{{ formatMoney(Number(value)) }}</template>
      <template #cell-receivable="{ value, row }">
        <span v-if="Number(value) <= 0" class="text-text-muted">—</span>
        <span v-else :class="Number(row.overdue) > 0 ? 'text-negative' : 'text-text'">
          {{ formatMoney(Number(value)) }}
        </span>
      </template>
      <template #cell-isActive="{ value }">
        <StatusBadge :label="value ? t('masterData.active') : t('masterData.inactive')" :tone="value ? 'positive' : 'neutral'" />
      </template>
      <template #cell-actions="{ row }">
        <span @click.stop>
          <RowActions
            :row="(row as unknown as Customer)"
            @edit="openEdit(row as unknown as Customer)"
            @toggle="toggleActive(row as unknown as Customer)"
          />
        </span>
      </template>
    </DataTable>

    <AppModal v-model="modalOpen" :title="editingId ? t('masterData.customers.edit') : t('masterData.customers.new')">
      <div class="space-y-4">
        <div class="grid grid-cols-2 gap-4">
          <FormField :label="t('masterData.fields.code')" required><AppInput v-model="form.code" :disabled="!!editingId" /></FormField>
          <FormField :label="t('masterData.fields.name')" required><AppInput v-model="form.name" /></FormField>
        </div>
        <FormField :label="t('masterData.fields.taxId')"><AppInput v-model="form.taxId" /></FormField>
        <div class="grid grid-cols-2 gap-4">
          <FormField :label="t('masterData.fields.terms')">
            <input v-model.number="form.paymentTermDays" type="number" min="0" class="field-input text-right tnum" />
          </FormField>
          <FormField :label="t('masterData.fields.creditLimit')">
            <input v-model.number="form.creditLimit" type="number" min="0" step="any" class="field-input text-right tnum" />
          </FormField>
        </div>
        <FormField v-if="editingId" :label="t('priceLists.assign.sales')">
          <AppSelect v-model="form.salesPriceListId" :options="priceListOptions" />
          <p class="mt-1 text-xs text-text-muted">{{ t('priceLists.assign.hint') }}</p>
        </FormField>
        <p v-if="error" class="text-sm text-negative">{{ error }}</p>
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="modalOpen = false">{{ t('masterData.cancel') }}</AppButton>
        <AppButton :disabled="saving || !form.code || !form.name" @click="save">
          {{ saving ? t('masterData.saving') : t('masterData.save') }}
        </AppButton>
      </template>
    </AppModal>

    <CsvImportModal
      v-model="importOpen"
      :title="`${t('masterData.import.import')} — ${t('masterData.tabs.customers')}`"
      :preview="importPreview"
      :busy="importBusy"
      :error="importError"
      :can-commit="canCommit"
      @confirm="commitImport"
    />
  </div>
</template>
