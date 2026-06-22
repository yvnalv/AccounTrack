<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { Plus, Upload, FileDown } from 'lucide-vue-next'
import { masterData } from '@/lib/masterData'
import { useCsvImport } from '@/composables/useCsvImport'
import type { Supplier } from '@/types/masterdata'
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

const { t } = useI18n()
const rows = ref<Supplier[]>([])

const insights = computed<Insight[]>(() => {
  const total = rows.value.length
  const active = rows.value.filter((r) => r.isActive).length
  return [
    { label: t('masterData.tabs.suppliers'), value: String(total) },
    { label: t('common.insights.active'), value: String(active), tone: 'positive' },
    { label: t('common.insights.inactive'), value: String(total - active), tone: total - active > 0 ? 'negative' : 'neutral' },
  ]
})

const io = masterData.supplierImport
const { fileInput, open: importOpen, preview: importPreview, busy: importBusy, error: importError, canCommit, pick, onFileChosen, commit: commitImport } =
  useCsvImport(io, () => load())
const loading = ref(true)
const modalOpen = ref(false)
const saving = ref(false)
const error = ref('')
const editingId = ref<string | null>(null)
const form = reactive({ code: '', name: '', taxId: '', paymentTermDays: 30 })

const columns = computed<Column[]>(() => [
  { key: 'code', label: t('masterData.fields.code') },
  { key: 'name', label: t('masterData.fields.name') },
  { key: 'taxId', label: t('masterData.fields.taxId') },
  { key: 'paymentTermDays', label: t('masterData.fields.terms'), align: 'right', numeric: true },
  { key: 'isActive', label: t('masterData.status') },
  { key: 'actions', label: t('masterData.actions'), align: 'right' },
])

async function load() {
  loading.value = true
  try {
    rows.value = await masterData.suppliers()
  } finally {
    loading.value = false
  }
}
onMounted(load)

function openNew() {
  editingId.value = null
  Object.assign(form, { code: '', name: '', taxId: '', paymentTermDays: 30 })
  error.value = ''
  modalOpen.value = true
}

function openEdit(row: Supplier) {
  editingId.value = row.id
  Object.assign(form, { code: row.code, name: row.name, taxId: row.taxId ?? '', paymentTermDays: row.paymentTermDays })
  error.value = ''
  modalOpen.value = true
}

async function save() {
  error.value = ''
  saving.value = true
  try {
    if (editingId.value) {
      await masterData.updateSupplier(editingId.value, {
        name: form.name,
        taxId: form.taxId || null,
        paymentTermDays: form.paymentTermDays,
      })
    } else {
      await masterData.createSupplier({
        code: form.code,
        name: form.name,
        taxId: form.taxId || null,
        paymentTermDays: form.paymentTermDays,
      })
    }
    modalOpen.value = false
    await load()
  } catch {
    error.value = t('masterData.failed')
  } finally {
    saving.value = false
  }
}

async function toggleActive(row: Supplier) {
  await masterData.setSupplierActive(row.id, !row.isActive)
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
      <ExportMenu :download="(f) => io.export(f)" />
      <button class="inline-flex items-center gap-1.5 rounded-button border border-border px-3 py-2 text-sm font-medium text-text-muted transition-colors hover:text-text hover:bg-surface-2" @click="pick">
        <Upload :size="16" /> {{ t('masterData.import.import') }}
      </button>
      <AppButton @click="openNew"><Plus :size="16" /> {{ t('masterData.suppliers.new') }}</AppButton>
    </div>

    <DataTable searchable :columns="columns" :rows="rows" :loading="loading" :empty-text="t('masterData.empty')">
      <template #cell-taxId="{ value }">{{ value || '—' }}</template>
      <template #cell-isActive="{ value }">
        <StatusBadge :label="value ? t('masterData.active') : t('masterData.inactive')" :tone="value ? 'positive' : 'neutral'" />
      </template>
      <template #cell-actions="{ row }">
        <RowActions :row="(row as unknown as Supplier)" @edit="openEdit(row as unknown as Supplier)" @toggle="toggleActive(row as unknown as Supplier)" />
      </template>
    </DataTable>

    <AppModal v-model="modalOpen" :title="editingId ? t('masterData.suppliers.edit') : t('masterData.suppliers.new')">
      <div class="space-y-4">
        <div class="grid grid-cols-2 gap-4">
          <FormField :label="t('masterData.fields.code')" required><AppInput v-model="form.code" :disabled="!!editingId" /></FormField>
          <FormField :label="t('masterData.fields.name')" required><AppInput v-model="form.name" /></FormField>
        </div>
        <FormField :label="t('masterData.fields.taxId')"><AppInput v-model="form.taxId" /></FormField>
        <FormField :label="t('masterData.fields.terms')">
          <input v-model.number="form.paymentTermDays" type="number" min="0" class="field-input text-right tnum" />
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
      :title="`${t('masterData.import.import')} — ${t('masterData.tabs.suppliers')}`"
      :preview="importPreview"
      :busy="importBusy"
      :error="importError"
      :can-commit="canCommit"
      @confirm="commitImport"
    />
  </div>
</template>
