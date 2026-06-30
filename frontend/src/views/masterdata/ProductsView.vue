<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { Check, Minus, Plus, Upload, FileDown } from 'lucide-vue-next'
import { masterData } from '@/lib/masterData'
import { isConflict } from '@/lib/api'
import { useCsvImport } from '@/composables/useCsvImport'
import type { NamedRef, Product } from '@/types/masterdata'
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
const auth = useAuthStore()
const rows = ref<Product[]>([])

const insights = computed<Insight[]>(() => {
  const total = rows.value.length
  const active = rows.value.filter((r) => r.isActive).length
  return [
    { label: t('masterData.tabs.products'), value: String(total) },
    { label: t('common.insights.active'), value: String(active), tone: 'positive' },
    { label: t('common.insights.inactive'), value: String(total - active), tone: total - active > 0 ? 'negative' : 'neutral' },
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
})

const columns = computed<Column[]>(() => [
  { key: 'code', label: t('masterData.fields.code') },
  { key: 'name', label: t('masterData.fields.name') },
  { key: 'isStockTracked', label: t('masterData.fields.stockTracked') },
  { key: 'isSold', label: t('masterData.fields.sold') },
  { key: 'isPurchased', label: t('masterData.fields.purchased') },
  { key: 'isActive', label: t('masterData.status') },
  { key: 'actions', label: t('masterData.actions'), align: 'right' },
])

const uomOptions = computed(() => uoms.value.map((u) => ({ value: u.id, label: `${u.code} — ${u.name}` })))
const categoryOptions = computed(() => [
  { value: '', label: t('masterData.products.noCategory') },
  ...categories.value.map((c) => ({ value: c.id, label: `${c.code} — ${c.name}` })),
])

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
      <ExportMenu :download="(f) => io.export(f)" />
      <button class="inline-flex items-center gap-1.5 rounded-button border border-border px-3 py-2 text-sm font-medium text-text-muted transition-colors hover:text-text hover:bg-surface-2" @click="pick">
        <Upload :size="16" /> {{ t('masterData.import.import') }}
      </button>
      <AppButton v-if="auth.has('MasterData.Create')" @click="openNew"><Plus :size="16" /> {{ t('masterData.products.new') }}</AppButton>
    </div>

    <DataTable searchable :columns="columns" :rows="rows" :loading="loading" :empty-text="t('masterData.empty')">
      <template #cell-isStockTracked="{ value }">
        <Check v-if="value" :size="16" class="text-positive" /><Minus v-else :size="16" class="text-text-muted" />
      </template>
      <template #cell-isSold="{ value }">
        <Check v-if="value" :size="16" class="text-positive" /><Minus v-else :size="16" class="text-text-muted" />
      </template>
      <template #cell-isPurchased="{ value }">
        <Check v-if="value" :size="16" class="text-positive" /><Minus v-else :size="16" class="text-text-muted" />
      </template>
      <template #cell-isActive="{ value }">
        <StatusBadge :label="value ? t('masterData.active') : t('masterData.inactive')" :tone="value ? 'positive' : 'neutral'" />
      </template>
      <template #cell-actions="{ row }">
        <RowActions :row="(row as unknown as Product)" @edit="openEdit(row as unknown as Product)" @toggle="toggleActive(row as unknown as Product)" />
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
