<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { Plus } from 'lucide-vue-next'
import { masterData } from '@/lib/masterData'
import { isConflict } from '@/lib/api'
import type { UnitOfMeasure } from '@/types/masterdata'
import AppButton from '@/components/ui/AppButton.vue'
import AppInput from '@/components/ui/AppInput.vue'
import AppModal from '@/components/ui/AppModal.vue'
import DataTable from '@/components/ui/DataTable.vue'
import FormField from '@/components/ui/FormField.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import RowActions from '@/components/ui/RowActions.vue'
import type { Column } from '@/components/ui/types'
import { useAuthStore } from '@/stores/auth'

const { t } = useI18n()
const auth = useAuthStore()
const rows = ref<UnitOfMeasure[]>([])
const loading = ref(true)
const modalOpen = ref(false)
const saving = ref(false)
const error = ref('')
const editingId = ref<string | null>(null)
const editRowVersion = ref<string | null>(null)
const form = reactive({ code: '', name: '' })

const columns = computed<Column[]>(() => [
  { key: 'code', label: t('masterData.fields.code') },
  { key: 'name', label: t('masterData.fields.name') },
  { key: 'isActive', label: t('masterData.status') },
  { key: 'actions', label: t('masterData.actions'), align: 'right' },
])

async function load() {
  loading.value = true
  try {
    rows.value = await masterData.unitsOfMeasure()
  } finally {
    loading.value = false
  }
}
onMounted(load)

function openNew() {
  editingId.value = null
  Object.assign(form, { code: '', name: '' })
  error.value = ''
  modalOpen.value = true
}

function openEdit(row: UnitOfMeasure) {
  editingId.value = row.id
  editRowVersion.value = row.rowVersion
  Object.assign(form, { code: row.code, name: row.name })
  error.value = ''
  modalOpen.value = true
}

async function save() {
  error.value = ''
  saving.value = true
  try {
    if (editingId.value) await masterData.updateUom(editingId.value, { name: form.name }, editRowVersion.value)
    else await masterData.createUom({ code: form.code, name: form.name })
    modalOpen.value = false
    await load()
  } catch (e) {
    error.value = isConflict(e) ? t('masterData.conflict') : t('masterData.failed')
  } finally {
    saving.value = false
  }
}

async function toggleActive(row: UnitOfMeasure) {
  await masterData.setUomActive(row.id, !row.isActive)
  await load()
}
</script>

<template>
  <div class="space-y-4">
    <div class="flex justify-end">
      <AppButton v-if="auth.has('MasterData.Create')" @click="openNew"><Plus :size="16" /> {{ t('masterData.unitsOfMeasure.new') }}</AppButton>
    </div>

    <DataTable searchable :columns="columns" :rows="rows" :loading="loading" :empty-text="t('masterData.empty')">
      <template #cell-isActive="{ value }">
        <StatusBadge :label="value ? t('masterData.active') : t('masterData.inactive')" :tone="value ? 'positive' : 'neutral'" />
      </template>
      <template #cell-actions="{ row }">
        <RowActions :row="(row as unknown as UnitOfMeasure)" @edit="openEdit(row as unknown as UnitOfMeasure)" @toggle="toggleActive(row as unknown as UnitOfMeasure)" />
      </template>
    </DataTable>

    <AppModal v-model="modalOpen" :title="editingId ? t('masterData.unitsOfMeasure.edit') : t('masterData.unitsOfMeasure.new')">
      <div class="grid grid-cols-2 gap-4">
        <FormField :label="t('masterData.fields.code')" required><AppInput v-model="form.code" :disabled="!!editingId" /></FormField>
        <FormField :label="t('masterData.fields.name')" required><AppInput v-model="form.name" /></FormField>
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
