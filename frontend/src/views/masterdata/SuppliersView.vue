<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { Plus } from 'lucide-vue-next'
import { masterData } from '@/lib/masterData'
import type { Supplier } from '@/types/masterdata'
import AppButton from '@/components/ui/AppButton.vue'
import AppInput from '@/components/ui/AppInput.vue'
import AppModal from '@/components/ui/AppModal.vue'
import DataTable from '@/components/ui/DataTable.vue'
import FormField from '@/components/ui/FormField.vue'
import type { Column } from '@/components/ui/types'

const { t } = useI18n()
const rows = ref<Supplier[]>([])
const loading = ref(true)
const modalOpen = ref(false)
const saving = ref(false)
const error = ref('')
const form = reactive({ code: '', name: '', taxId: '', paymentTermDays: 30 })

const columns = computed<Column[]>(() => [
  { key: 'code', label: t('masterData.fields.code') },
  { key: 'name', label: t('masterData.fields.name') },
  { key: 'taxId', label: t('masterData.fields.taxId') },
  { key: 'paymentTermDays', label: t('masterData.fields.terms'), align: 'right', numeric: true },
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
  Object.assign(form, { code: '', name: '', taxId: '', paymentTermDays: 30 })
  error.value = ''
  modalOpen.value = true
}

async function save() {
  error.value = ''
  saving.value = true
  try {
    await masterData.createSupplier({
      code: form.code,
      name: form.name,
      taxId: form.taxId || null,
      paymentTermDays: form.paymentTermDays,
    })
    modalOpen.value = false
    await load()
  } catch {
    error.value = t('masterData.failed')
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="space-y-4">
    <div class="flex justify-end">
      <AppButton @click="openNew"><Plus :size="16" /> {{ t('masterData.suppliers.new') }}</AppButton>
    </div>

    <DataTable :columns="columns" :rows="rows" :loading="loading" :empty-text="t('masterData.empty')">
      <template #cell-taxId="{ value }">{{ value || '—' }}</template>
    </DataTable>

    <AppModal v-model="modalOpen" :title="t('masterData.suppliers.new')">
      <div class="space-y-4">
        <div class="grid grid-cols-2 gap-4">
          <FormField :label="t('masterData.fields.code')" required><AppInput v-model="form.code" /></FormField>
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
  </div>
</template>
