<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { Plus, Upload, FileDown } from 'lucide-vue-next'
import { masterData } from '@/lib/masterData'
import { inventoryApi } from '@/lib/inventory'
import { isConflict } from '@/lib/api'
import { exportTable } from '@/lib/exportTable'
import { formatMoney, formatMoneyShort, formatNumber } from '@/lib/format'
import { useCsvImport } from '@/composables/useCsvImport'
import type { Warehouse } from '@/types/masterdata'
import AppButton from '@/components/ui/AppButton.vue'
import AppInput from '@/components/ui/AppInput.vue'
import AppModal from '@/components/ui/AppModal.vue'
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
const rows = ref<Warehouse[]>([])
const filteredRows = ref<Record<string, unknown>[]>([])

function openDetail(row: Record<string, unknown>) {
  router.push({ name: 'masterDataWarehouseDetail', params: { id: String(row.id) } })
}

// Stock value + SKU count per warehouse, from the inventory ledger.
const valueByWarehouse = ref(new Map<string, number>())
const skusByWarehouse = ref(new Map<string, number>())
const stockValueTotal = ref(0)

const insights = computed<Insight[]>(() => {
  const total = rows.value.length
  const active = rows.value.filter((r) => r.isActive).length
  const skuTotal = [...skusByWarehouse.value.values()].reduce((s, n) => s + n, 0)
  return [
    { label: t('masterData.tabs.warehouses'), value: String(total) },
    { label: t('common.insights.active'), value: String(active), tone: 'positive' },
    { label: t('masterData.warehouses.stockValue'), value: formatMoneyShort(stockValueTotal.value), tone: 'accent' },
    { label: t('masterData.warehouses.skus'), value: String(skuTotal) },
  ]
})

const tableRows = computed(() =>
  rows.value.map((w) => ({
    ...w,
    stockValue: valueByWarehouse.value.get(w.id) ?? 0,
    skus: skusByWarehouse.value.get(w.id) ?? 0,
  })),
)

const io = masterData.warehouseImport
const { fileInput, open: importOpen, preview: importPreview, busy: importBusy, error: importError, canCommit, pick, onFileChosen, commit: commitImport } =
  useCsvImport(io, () => load())
const loading = ref(true)
const modalOpen = ref(false)
const saving = ref(false)
const error = ref('')
const editingId = ref<string | null>(null)
const editRowVersion = ref<string | null>(null)
const form = reactive({ code: '', name: '', address: '' })

const columns = computed<Column[]>(() => [
  { key: 'code', label: t('masterData.fields.code') },
  { key: 'name', label: t('masterData.fields.name') },
  { key: 'address', label: t('masterData.fields.address') },
  { key: 'skus', label: t('masterData.warehouses.skus'), align: 'right', numeric: true },
  { key: 'stockValue', label: t('masterData.warehouses.stockValue'), align: 'right', numeric: true },
  { key: 'isActive', label: t('masterData.status') },
  { key: 'actions', label: t('masterData.actions'), align: 'right' },
])

async function load() {
  loading.value = true
  try {
    rows.value = await masterData.warehouses()

    // Stock is cross-module (Inventory.View); degrade gracefully if the user lacks it.
    const onHand = await inventoryApi.onHand().catch(() => [])
    const value = new Map<string, number>()
    const skus = new Map<string, number>()
    let totalValue = 0
    for (const s of onHand) {
      if (s.onHandQty === 0 && s.value === 0) continue
      value.set(s.warehouseId, (value.get(s.warehouseId) ?? 0) + s.value)
      skus.set(s.warehouseId, (skus.get(s.warehouseId) ?? 0) + 1)
      totalValue += s.value
    }
    valueByWarehouse.value = value
    skusByWarehouse.value = skus
    stockValueTotal.value = totalValue
  } finally {
    loading.value = false
  }
}
onMounted(load)

function openNew() {
  editingId.value = null
  Object.assign(form, { code: '', name: '', address: '' })
  error.value = ''
  modalOpen.value = true
}

function openEdit(row: Warehouse) {
  editingId.value = row.id
  editRowVersion.value = row.rowVersion
  Object.assign(form, { code: row.code, name: row.name, address: row.address ?? '' })
  error.value = ''
  modalOpen.value = true
}

async function save() {
  error.value = ''
  saving.value = true
  try {
    if (editingId.value) {
      await masterData.updateWarehouse(editingId.value, { name: form.name, address: form.address || null }, editRowVersion.value)
    } else {
      await masterData.createWarehouse({ code: form.code, name: form.name, address: form.address || null })
    }
    modalOpen.value = false
    await load()
  } catch (e) {
    error.value = isConflict(e) ? t('masterData.conflict') : t('masterData.failed')
  } finally {
    saving.value = false
  }
}

async function toggleActive(row: Warehouse) {
  await masterData.setWarehouseActive(row.id, !row.isActive)
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
      <ExportMenu :download="(f) => exportTable(columns, filteredRows, 'warehouses', f)" />
      <button class="inline-flex items-center gap-1.5 rounded-button border border-border px-3 py-2 text-sm font-medium text-text-muted transition-colors hover:text-text hover:bg-surface-2" @click="pick">
        <Upload :size="16" /> {{ t('masterData.import.import') }}
      </button>
      <AppButton v-if="auth.has('MasterData.Create')" @click="openNew"><Plus :size="16" /> {{ t('masterData.warehouses.new') }}</AppButton>
    </div>

    <DataTable v-model:filtered="filteredRows" searchable clickable :columns="columns" :rows="tableRows" :loading="loading" :empty-text="t('masterData.empty')" @row-click="openDetail">
      <template #cell-address="{ value }">{{ value || '—' }}</template>
      <template #cell-skus="{ value }">{{ formatNumber(Number(value)) }}</template>
      <template #cell-stockValue="{ value }">
        <span :class="Number(value) > 0 ? 'text-text' : 'text-text-muted'">{{ formatMoney(Number(value)) }}</span>
      </template>
      <template #cell-isActive="{ value }">
        <StatusBadge :label="value ? t('masterData.active') : t('masterData.inactive')" :tone="value ? 'positive' : 'neutral'" />
      </template>
      <template #cell-actions="{ row }">
        <span @click.stop>
          <RowActions :row="(row as unknown as Warehouse)" @edit="openEdit(row as unknown as Warehouse)" @toggle="toggleActive(row as unknown as Warehouse)" />
        </span>
      </template>
    </DataTable>

    <AppModal v-model="modalOpen" :title="editingId ? t('masterData.warehouses.edit') : t('masterData.warehouses.new')">
      <div class="space-y-4">
        <div class="grid grid-cols-2 gap-4">
          <FormField :label="t('masterData.fields.code')" required><AppInput v-model="form.code" :disabled="!!editingId" /></FormField>
          <FormField :label="t('masterData.fields.name')" required><AppInput v-model="form.name" /></FormField>
        </div>
        <FormField :label="t('masterData.fields.address')"><AppInput v-model="form.address" /></FormField>
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
      :title="`${t('masterData.import.import')} — ${t('masterData.tabs.warehouses')}`"
      :preview="importPreview"
      :busy="importBusy"
      :error="importError"
      :can-commit="canCommit"
      @confirm="commitImport"
    />
  </div>
</template>
