<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { Plus } from 'lucide-vue-next'
import { masterData } from '@/lib/masterData'
import { formatPercent } from '@/lib/format'
import type { TaxCode } from '@/types/masterdata'
import AppButton from '@/components/ui/AppButton.vue'
import AppInput from '@/components/ui/AppInput.vue'
import AppModal from '@/components/ui/AppModal.vue'
import DataTable from '@/components/ui/DataTable.vue'
import FormField from '@/components/ui/FormField.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import RowActions from '@/components/ui/RowActions.vue'
import type { Column } from '@/components/ui/types'

const { t } = useI18n()
const rows = ref<TaxCode[]>([])
const loading = ref(true)
const modalOpen = ref(false)
const saving = ref(false)
const error = ref('')
const editingId = ref<string | null>(null)
const form = reactive({ code: '', name: '', ratePercent: 0 })

const columns = computed<Column[]>(() => [
  { key: 'code', label: t('masterData.fields.code') },
  { key: 'name', label: t('masterData.fields.name') },
  { key: 'rate', label: t('masterData.fields.rate'), align: 'right', numeric: true },
  { key: 'isActive', label: t('masterData.status') },
  { key: 'actions', label: t('masterData.actions'), align: 'right' },
])

async function load() {
  loading.value = true
  try {
    rows.value = await masterData.taxCodes()
  } finally {
    loading.value = false
  }
}
onMounted(load)

function openNew() {
  editingId.value = null
  Object.assign(form, { code: '', name: '', ratePercent: 0 })
  error.value = ''
  modalOpen.value = true
}

function openEdit(row: TaxCode) {
  editingId.value = row.id
  Object.assign(form, { code: row.code, name: row.name, ratePercent: row.rate * 100 })
  error.value = ''
  modalOpen.value = true
}

async function save() {
  error.value = ''
  saving.value = true
  try {
    const rate = Number(form.ratePercent) / 100
    if (editingId.value) await masterData.updateTaxCode(editingId.value, { name: form.name, rate })
    else await masterData.createTaxCode({ code: form.code, name: form.name, rate })
    modalOpen.value = false
    await load()
  } catch {
    error.value = t('masterData.failed')
  } finally {
    saving.value = false
  }
}

async function toggleActive(row: TaxCode) {
  await masterData.setTaxCodeActive(row.id, !row.isActive)
  await load()
}
</script>

<template>
  <div class="space-y-4">
    <div class="flex justify-end">
      <AppButton @click="openNew"><Plus :size="16" /> {{ t('masterData.taxCodes.new') }}</AppButton>
    </div>

    <DataTable searchable :columns="columns" :rows="rows" :loading="loading" :empty-text="t('masterData.empty')">
      <template #cell-rate="{ value }">{{ formatPercent(Number(value) * 100) }}</template>
      <template #cell-isActive="{ value }">
        <StatusBadge :label="value ? t('masterData.active') : t('masterData.inactive')" :tone="value ? 'positive' : 'neutral'" />
      </template>
      <template #cell-actions="{ row }">
        <RowActions :row="(row as unknown as TaxCode)" @edit="openEdit(row as unknown as TaxCode)" @toggle="toggleActive(row as unknown as TaxCode)" />
      </template>
    </DataTable>

    <AppModal v-model="modalOpen" :title="editingId ? t('masterData.taxCodes.edit') : t('masterData.taxCodes.new')">
      <div class="grid grid-cols-3 gap-4">
        <FormField :label="t('masterData.fields.code')" required><AppInput v-model="form.code" :disabled="!!editingId" /></FormField>
        <FormField :label="t('masterData.fields.name')" required><AppInput v-model="form.name" /></FormField>
        <FormField :label="t('masterData.fields.rate')" required>
          <input v-model.number="form.ratePercent" type="number" min="0" max="100" step="any" class="field-input text-right tnum" />
        </FormField>
      </div>
      <p v-if="error" class="mt-3 text-sm text-negative">{{ error }}</p>
      <template #footer>
        <AppButton variant="ghost" @click="modalOpen = false">{{ t('masterData.cancel') }}</AppButton>
        <AppButton :disabled="saving || !form.code || !form.name" @click="save">
          {{ saving ? t('masterData.saving') : t('masterData.save') }}
        </AppButton>
      </template>
    </AppModal>
  </div>
</template>
