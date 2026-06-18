<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { Plus } from 'lucide-vue-next'
import { masterData } from '@/lib/masterData'
import type { Warehouse } from '@/types/masterdata'
import AppButton from '@/components/ui/AppButton.vue'
import AppInput from '@/components/ui/AppInput.vue'
import AppModal from '@/components/ui/AppModal.vue'
import DataTable from '@/components/ui/DataTable.vue'
import FormField from '@/components/ui/FormField.vue'
import type { Column } from '@/components/ui/types'

const { t } = useI18n()
const rows = ref<Warehouse[]>([])
const loading = ref(true)
const modalOpen = ref(false)
const saving = ref(false)
const error = ref('')
const form = reactive({ code: '', name: '', address: '' })

const columns = computed<Column[]>(() => [
  { key: 'code', label: t('masterData.fields.code') },
  { key: 'name', label: t('masterData.fields.name') },
  { key: 'address', label: t('masterData.fields.address') },
])

async function load() {
  loading.value = true
  try {
    rows.value = await masterData.warehouses()
  } finally {
    loading.value = false
  }
}
onMounted(load)

function openNew() {
  Object.assign(form, { code: '', name: '', address: '' })
  error.value = ''
  modalOpen.value = true
}

async function save() {
  error.value = ''
  saving.value = true
  try {
    await masterData.createWarehouse({ code: form.code, name: form.name, address: form.address || null })
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
      <AppButton @click="openNew"><Plus :size="16" /> {{ t('masterData.warehouses.new') }}</AppButton>
    </div>

    <DataTable :columns="columns" :rows="rows" :loading="loading" :empty-text="t('masterData.empty')">
      <template #cell-address="{ value }">{{ value || '—' }}</template>
    </DataTable>

    <AppModal v-model="modalOpen" :title="t('masterData.warehouses.new')">
      <div class="space-y-4">
        <div class="grid grid-cols-2 gap-4">
          <FormField :label="t('masterData.fields.code')" required><AppInput v-model="form.code" /></FormField>
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
  </div>
</template>
