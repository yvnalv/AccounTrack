<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { Check, Minus, Plus, Upload, FileDown } from 'lucide-vue-next'
import { masterData } from '@/lib/masterData'
import { inventoryApi } from '@/lib/inventory'
import { isConflict } from '@/lib/api'
import { formatMoney, formatMoneyShort, formatNumber } from '@/lib/format'
import { exportTable } from '@/lib/exportTable'
import { useCsvImport } from '@/composables/useCsvImport'
import type { CostingMethod, NamedRef, Product } from '@/types/masterdata'
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
const rows = ref<Product[]>([])
const filteredRows = ref<Record<string, unknown>[]>([])

function openDetail(row: Record<string, unknown>) {
  router.push({ name: 'masterDataProductDetail', params: { id: String(row.id) } })
}

// Current stock per product (summed across warehouses), from the inventory ledger.
const stockQtyByProduct = ref(new Map<string, number>())
const stockValueByProduct = ref(new Map<string, number>())
const stockValueTotal = ref(0)

const insights = computed<Insight[]>(() => {
  const total = rows.value.length
  const active = rows.value.filter((r) => r.isActive).length
  const outOfStock = rows.value.filter(
    (r) => r.isActive && r.isStockTracked && (stockQtyByProduct.value.get(r.id) ?? 0) <= 0,
  ).length
  return [
    { label: t('masterData.tabs.products'), value: String(total) },
    { label: t('common.insights.active'), value: String(active), tone: 'positive' },
    { label: t('masterData.products.stockValue'), value: formatMoneyShort(stockValueTotal.value), tone: 'accent' },
    { label: t('masterData.products.outOfStock'), value: String(outOfStock), tone: outOfStock > 0 ? 'negative' : 'neutral' },
  ]
})

const io = masterData.productImport
const { fileInput, open: importOpen, preview: importPreview, busy: importBusy, error: importError, canCommit, pick, onFileChosen, commit: commitImport } =
  useCsvImport(io, () => load())
const uoms = ref<NamedRef[]>([])
const categories = ref<NamedRef[]>([])
const loading = ref(true)
const modalOpen = ref(false)
const saving = ref(false)
const error = ref('')
const editingId = ref<string | null>(null)
const editRowVersion = ref<string | null>(null)
const form = reactive({
  code: '',
  name: '',
  baseUomId: '',
  categoryId: '',
  isStockTracked: true,
  isSold: true,
  isPurchased: true,
  costingMethod: 'MovingAverage' as CostingMethod,
  salePrice: null as number | null,
  purchasePrice: null as number | null,
})

const costingMethodOptions = computed(() => [
  { value: 'MovingAverage', label: t('masterData.products.costing.movingAverage') },
  { value: 'Fifo', label: t('masterData.products.costing.fifo') },
])

const columns = computed<Column[]>(() => [
  { key: 'code', label: t('masterData.fields.code') },
  { key: 'name', label: t('masterData.fields.name') },
  { key: 'categoryName', label: t('masterData.fields.category') },
  { key: 'uomCode', label: t('masterData.fields.uom') },
  { key: 'currentStock', label: t('masterData.products.currentStock'), align: 'right', numeric: true },
  { key: 'salePrice', label: t('masterData.products.salePrice'), align: 'right', numeric: true },
  { key: 'purchasePrice', label: t('masterData.products.purchasePrice'), align: 'right', numeric: true },
  { key: 'costingMethod', label: t('masterData.products.costing.label') },
  { key: 'isStockTracked', label: t('masterData.fields.stockTracked') },
  { key: 'isActive', label: t('masterData.status') },
  { key: 'actions', label: t('masterData.actions'), align: 'right' },
])

const uomOptions = computed(() => uoms.value.map((u) => ({ value: u.id, label: `${u.code} — ${u.name}` })))
const categoryOptions = computed(() => [
  { value: '', label: t('masterData.products.noCategory') },
  ...categories.value.map((c) => ({ value: c.id, label: `${c.code} — ${c.name}` })),
])

// Enrich rows with resolved category name + UoM code so those columns are readable and searchable.
const uomCodeById = computed(() => new Map(uoms.value.map((u) => [u.id, u.code])))
const categoryNameById = computed(() => new Map(categories.value.map((c) => [c.id, c.name])))
const tableRows = computed(() =>
  rows.value.map((p) => ({
    ...p,
    categoryName: (p.categoryId && categoryNameById.value.get(p.categoryId)) || '—',
    uomCode: uomCodeById.value.get(p.baseUomId) ?? '—',
    currentStock: p.isStockTracked ? (stockQtyByProduct.value.get(p.id) ?? 0) : null,
  })),
)

async function load() {
  loading.value = true
  try {
    const [p, u, c] = await Promise.all([
      masterData.products(),
      masterData.unitsOfMeasure(),
      masterData.productCategories(),
    ])
    rows.value = p
    uoms.value = u
    categories.value = c

    // Stock is cross-module (Inventory.View); degrade gracefully if the user lacks it.
    const onHand = await inventoryApi.onHand().catch(() => [])
    const qty = new Map<string, number>()
    const value = new Map<string, number>()
    let totalValue = 0
    for (const s of onHand) {
      qty.set(s.productId, (qty.get(s.productId) ?? 0) + s.onHandQty)
      value.set(s.productId, (value.get(s.productId) ?? 0) + s.value)
      totalValue += s.value
    }
    stockQtyByProduct.value = qty
    stockValueByProduct.value = value
    stockValueTotal.value = totalValue
  } finally {
    loading.value = false
  }
}
onMounted(load)

function openNew() {
  editingId.value = null
  Object.assign(form, {
    code: '',
    name: '',
    baseUomId: uoms.value[0]?.id ?? '',
    categoryId: '',
    isStockTracked: true,
    isSold: true,
    isPurchased: true,
    costingMethod: 'MovingAverage' as CostingMethod,
    salePrice: null,
    purchasePrice: null,
  })
  error.value = ''
  modalOpen.value = true
}

function openEdit(row: Product) {
  editingId.value = row.id
  editRowVersion.value = row.rowVersion
  Object.assign(form, {
    code: row.code,
    name: row.name,
    baseUomId: row.baseUomId,
    categoryId: row.categoryId ?? '',
    isStockTracked: row.isStockTracked,
    isSold: row.isSold,
    isPurchased: row.isPurchased,
    costingMethod: row.costingMethod ?? 'MovingAverage',
    salePrice: row.salePrice,
    purchasePrice: row.purchasePrice,
  })
  error.value = ''
  modalOpen.value = true
}

async function save() {
  error.value = ''
  saving.value = true
  try {
    if (editingId.value) {
      await masterData.updateProduct(editingId.value, {
        name: form.name,
        categoryId: form.categoryId || null,
        isStockTracked: form.isStockTracked,
        isSold: form.isSold,
        isPurchased: form.isPurchased,
        salePrice: form.salePrice,
        purchasePrice: form.purchasePrice,
      }, editRowVersion.value)
    } else {
      await masterData.createProduct({
        code: form.code,
        name: form.name,
        baseUomId: form.baseUomId,
        categoryId: form.categoryId || null,
        isStockTracked: form.isStockTracked,
        isSold: form.isSold,
        isPurchased: form.isPurchased,
        costingMethod: form.costingMethod,
        salePrice: form.salePrice,
        purchasePrice: form.purchasePrice,
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

async function toggleActive(row: Product) {
  await masterData.setProductActive(row.id, !row.isActive)
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
      <ExportMenu :download="(f) => exportTable(columns, filteredRows, 'products', f)" />
      <button class="inline-flex items-center gap-1.5 rounded-button border border-border px-3 py-2 text-sm font-medium text-text-muted transition-colors hover:text-text hover:bg-surface-2" @click="pick">
        <Upload :size="16" /> {{ t('masterData.import.import') }}
      </button>
      <AppButton v-if="auth.has('MasterData.Create')" @click="openNew"><Plus :size="16" /> {{ t('masterData.products.new') }}</AppButton>
    </div>

    <DataTable v-model:filtered="filteredRows" searchable clickable :columns="columns" :rows="tableRows" :loading="loading" :empty-text="t('masterData.empty')" @row-click="openDetail">
      <template #cell-currentStock="{ value, row }">
        <span v-if="value == null" class="text-text-muted">—</span>
        <span v-else :class="Number(value) <= 0 ? 'text-negative' : 'text-text'">
          {{ formatNumber(Number(value)) }}
          <span class="text-xs text-text-muted">{{ row.uomCode }}</span>
        </span>
      </template>
      <template #cell-salePrice="{ value }">
        <span :class="value == null ? 'text-text-muted' : 'text-text'">{{ value == null ? '—' : formatMoney(Number(value)) }}</span>
      </template>
      <template #cell-purchasePrice="{ value }">
        <span :class="value == null ? 'text-text-muted' : 'text-text'">{{ value == null ? '—' : formatMoney(Number(value)) }}</span>
      </template>
      <template #cell-costingMethod="{ value }">
        {{ value === 'Fifo' ? t('masterData.products.costing.fifo') : t('masterData.products.costing.movingAverage') }}
      </template>
      <template #cell-isStockTracked="{ value }">
        <Check v-if="value" :size="16" class="text-positive" /><Minus v-else :size="16" class="text-text-muted" />
      </template>
      <template #cell-isActive="{ value }">
        <StatusBadge :label="value ? t('masterData.active') : t('masterData.inactive')" :tone="value ? 'positive' : 'neutral'" />
      </template>
      <template #cell-actions="{ row }">
        <span @click.stop>
          <RowActions :row="(row as unknown as Product)" @edit="openEdit(row as unknown as Product)" @toggle="toggleActive(row as unknown as Product)" />
        </span>
      </template>
    </DataTable>

    <AppModal v-model="modalOpen" :title="editingId ? t('masterData.products.edit') : t('masterData.products.new')">
      <div class="space-y-4">
        <div class="grid grid-cols-2 gap-4">
          <FormField :label="t('masterData.fields.code')" required><AppInput v-model="form.code" :disabled="!!editingId" /></FormField>
          <FormField :label="t('masterData.fields.name')" required><AppInput v-model="form.name" /></FormField>
        </div>
        <div class="grid grid-cols-2 gap-4">
          <FormField :label="t('masterData.fields.uom')" required>
            <AppSelect v-model="form.baseUomId" :options="uomOptions" :placeholder="t('masterData.products.selectUom')" :disabled="!!editingId" />
          </FormField>
          <FormField :label="t('masterData.fields.category')">
            <AppSelect v-model="form.categoryId" :options="categoryOptions" />
          </FormField>
        </div>
        <div class="grid grid-cols-2 gap-4">
          <FormField :label="t('masterData.products.costing.label')">
            <AppSelect v-model="form.costingMethod" :options="costingMethodOptions" :disabled="!!editingId" />
            <p class="mt-1 text-xs text-text-muted">
              {{ editingId ? t('masterData.products.costing.lockedHint') : t('masterData.products.costing.hint') }}
            </p>
          </FormField>
        </div>
        <div class="grid grid-cols-2 gap-4">
          <FormField :label="t('masterData.products.salePrice')">
            <input v-model.number="form.salePrice" type="number" min="0" step="any" class="field-input text-right tnum" :placeholder="t('masterData.products.pricePlaceholder')" />
          </FormField>
          <FormField :label="t('masterData.products.purchasePrice')">
            <input v-model.number="form.purchasePrice" type="number" min="0" step="any" class="field-input text-right tnum" :placeholder="t('masterData.products.pricePlaceholder')" />
          </FormField>
        </div>
        <p class="-mt-2 text-xs text-text-muted">{{ t('masterData.products.priceHint') }}</p>
        <div class="flex flex-wrap gap-4">
          <label class="flex items-center gap-2 text-sm text-text">
            <input v-model="form.isStockTracked" type="checkbox" class="h-4 w-4 accent-accent" /> {{ t('masterData.fields.stockTracked') }}
          </label>
          <label class="flex items-center gap-2 text-sm text-text">
            <input v-model="form.isSold" type="checkbox" class="h-4 w-4 accent-accent" /> {{ t('masterData.fields.sold') }}
          </label>
          <label class="flex items-center gap-2 text-sm text-text">
            <input v-model="form.isPurchased" type="checkbox" class="h-4 w-4 accent-accent" /> {{ t('masterData.fields.purchased') }}
          </label>
        </div>
        <p v-if="error" class="text-sm text-negative">{{ error }}</p>
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="modalOpen = false">{{ t('masterData.cancel') }}</AppButton>
        <AppButton :disabled="saving || !form.code || !form.name || !form.baseUomId" @click="save">
          {{ saving ? t('masterData.saving') : t('masterData.save') }}
        </AppButton>
      </template>
    </AppModal>

    <CsvImportModal
      v-model="importOpen"
      :title="`${t('masterData.import.import')} — ${t('masterData.tabs.products')}`"
      :preview="importPreview"
      :busy="importBusy"
      :error="importError"
      :can-commit="canCommit"
      @confirm="commitImport"
    />
  </div>
</template>
