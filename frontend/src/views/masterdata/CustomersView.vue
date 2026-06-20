<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { Plus, Upload, Download, FileDown } from 'lucide-vue-next'
import { masterData } from '@/lib/masterData'
import { formatMoney } from '@/lib/format'
import type { Customer, ImportPreview } from '@/types/masterdata'
import AppButton from '@/components/ui/AppButton.vue'
import AppInput from '@/components/ui/AppInput.vue'
import AppModal from '@/components/ui/AppModal.vue'
import DataTable from '@/components/ui/DataTable.vue'
import FormField from '@/components/ui/FormField.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import RowActions from '@/components/ui/RowActions.vue'
import type { Column } from '@/components/ui/types'

const { t } = useI18n()
const rows = ref<Customer[]>([])
const loading = ref(true)
const modalOpen = ref(false)
const saving = ref(false)
const error = ref('')
const editingId = ref<string | null>(null)
const form = reactive({ code: '', name: '', taxId: '', paymentTermDays: 30, creditLimit: 0 })

// --- Import ---
const fileInput = ref<HTMLInputElement | null>(null)
const importModal = ref(false)
const importPreview = ref<ImportPreview | null>(null)
const importFile = ref<File | null>(null)
const importBusy = ref(false)
const importError = ref('')

function pickFile() {
  fileInput.value?.click()
}

async function onFileChosen(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = '' // allow re-selecting the same file
  if (!file) return
  importFile.value = file
  importError.value = ''
  importPreview.value = null
  importBusy.value = true
  importModal.value = true
  try {
    importPreview.value = await masterData.previewCustomerImport(file)
  } catch {
    importError.value = t('masterData.import.failed')
  } finally {
    importBusy.value = false
  }
}

const canCommit = computed(() => !!importPreview.value && importPreview.value.errorRows === 0 && importPreview.value.totalRows > 0)

async function commitImport() {
  if (!importFile.value) return
  importBusy.value = true
  importError.value = ''
  try {
    await masterData.commitCustomerImport(importFile.value)
    importModal.value = false
    await load()
  } catch {
    importError.value = t('masterData.import.failed')
  } finally {
    importBusy.value = false
  }
}

const columns = computed<Column[]>(() => [
  { key: 'code', label: t('masterData.fields.code') },
  { key: 'name', label: t('masterData.fields.name') },
  { key: 'taxId', label: t('masterData.fields.taxId') },
  { key: 'paymentTermDays', label: t('masterData.fields.terms'), align: 'right', numeric: true },
  { key: 'creditLimit', label: t('masterData.fields.creditLimit'), align: 'right', numeric: true },
  { key: 'isActive', label: t('masterData.status') },
  { key: 'actions', label: t('masterData.actions'), align: 'right' },
])

async function load() {
  loading.value = true
  try {
    rows.value = await masterData.customers()
  } finally {
    loading.value = false
  }
}
onMounted(load)

function openNew() {
  editingId.value = null
  Object.assign(form, { code: '', name: '', taxId: '', paymentTermDays: 30, creditLimit: 0 })
  error.value = ''
  modalOpen.value = true
}

function openEdit(row: Customer) {
  editingId.value = row.id
  Object.assign(form, {
    code: row.code,
    name: row.name,
    taxId: row.taxId ?? '',
    paymentTermDays: row.paymentTermDays,
    creditLimit: row.creditLimit,
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
      })
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
  } catch {
    error.value = t('masterData.failed')
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
    <div class="flex flex-wrap items-center justify-end gap-2">
      <input ref="fileInput" type="file" accept=".csv,text/csv" class="hidden" @change="onFileChosen" />
      <button class="inline-flex items-center gap-1.5 rounded-button border border-border px-3 py-2 text-sm font-medium text-text-muted transition-colors hover:text-text hover:bg-surface-2" @click="masterData.customerImportTemplate()">
        <FileDown :size="16" /> {{ t('masterData.import.template') }}
      </button>
      <button class="inline-flex items-center gap-1.5 rounded-button border border-border px-3 py-2 text-sm font-medium text-text-muted transition-colors hover:text-text hover:bg-surface-2" @click="masterData.exportCustomers()">
        <Download :size="16" /> {{ t('masterData.import.export') }}
      </button>
      <button class="inline-flex items-center gap-1.5 rounded-button border border-border px-3 py-2 text-sm font-medium text-text-muted transition-colors hover:text-text hover:bg-surface-2" @click="pickFile">
        <Upload :size="16" /> {{ t('masterData.import.import') }}
      </button>
      <AppButton @click="openNew"><Plus :size="16" /> {{ t('masterData.customers.new') }}</AppButton>
    </div>

    <DataTable :columns="columns" :rows="rows" :loading="loading" :empty-text="t('masterData.empty')">
      <template #cell-taxId="{ value }">{{ value || '—' }}</template>
      <template #cell-creditLimit="{ value }">{{ formatMoney(Number(value)) }}</template>
      <template #cell-isActive="{ value }">
        <StatusBadge :label="value ? t('masterData.active') : t('masterData.inactive')" :tone="value ? 'positive' : 'neutral'" />
      </template>
      <template #cell-actions="{ row }">
        <RowActions
          :row="(row as unknown as Customer)"
          @edit="openEdit(row as unknown as Customer)"
          @toggle="toggleActive(row as unknown as Customer)"
        />
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
        <p v-if="error" class="text-sm text-negative">{{ error }}</p>
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="modalOpen = false">{{ t('masterData.cancel') }}</AppButton>
        <AppButton :disabled="saving || !form.code || !form.name" @click="save">
          {{ saving ? t('masterData.saving') : t('masterData.save') }}
        </AppButton>
      </template>
    </AppModal>

    <AppModal v-model="importModal" :title="t('masterData.import.title')">
      <div class="space-y-3">
        <p v-if="importBusy && !importPreview" class="text-sm text-text-muted">{{ t('common.loading') }}</p>
        <template v-else-if="importPreview">
          <div class="flex flex-wrap gap-4 text-sm">
            <span class="text-text-muted">{{ t('masterData.import.rows') }}: <b class="text-text tnum">{{ importPreview.totalRows }}</b></span>
            <span class="text-positive">{{ t('masterData.import.create') }}: <b class="tnum">{{ importPreview.toCreate }}</b></span>
            <span class="text-info">{{ t('masterData.import.update') }}: <b class="tnum">{{ importPreview.toUpdate }}</b></span>
            <span :class="importPreview.errorRows ? 'text-negative' : 'text-text-muted'">{{ t('masterData.import.errors') }}: <b class="tnum">{{ importPreview.errorRows }}</b></span>
          </div>
          <div class="max-h-72 overflow-y-auto rounded-card border border-border">
            <table class="w-full text-sm">
              <thead>
                <tr class="border-b border-border bg-surface-2 text-xs uppercase tracking-wide text-text-muted">
                  <th class="px-3 py-2 text-left font-semibold">#</th>
                  <th class="px-3 py-2 text-left font-semibold">{{ t('masterData.fields.code') }}</th>
                  <th class="px-3 py-2 text-left font-semibold">{{ t('masterData.import.action') }}</th>
                  <th class="px-3 py-2 text-left font-semibold">{{ t('masterData.import.issues') }}</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="r in importPreview.rows" :key="r.rowNumber" class="border-b border-border last:border-0">
                  <td class="px-3 py-1.5 text-text-muted tnum">{{ r.rowNumber }}</td>
                  <td class="px-3 py-1.5 text-text">{{ r.key || '—' }}</td>
                  <td class="px-3 py-1.5">
                    <StatusBadge
                      :label="t(`masterData.import.actions.${r.action}`)"
                      :tone="r.action === 'Error' ? 'negative' : r.action === 'Update' ? 'info' : 'positive'"
                    />
                  </td>
                  <td class="px-3 py-1.5 text-negative">{{ r.errors.join(' ') }}</td>
                </tr>
              </tbody>
            </table>
          </div>
          <p v-if="importPreview.errorRows" class="text-xs text-text-muted">{{ t('masterData.import.allOrNothing') }}</p>
        </template>
        <p v-if="importError" class="text-sm text-negative">{{ importError }}</p>
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="importModal = false">{{ t('masterData.cancel') }}</AppButton>
        <AppButton :disabled="importBusy || !canCommit" @click="commitImport">
          {{ importBusy ? t('masterData.saving') : t('masterData.import.confirm') }}
        </AppButton>
      </template>
    </AppModal>
  </div>
</template>
