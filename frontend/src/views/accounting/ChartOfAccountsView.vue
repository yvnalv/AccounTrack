<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { Plus, Check, Minus, Pencil, Ban, RotateCcw } from 'lucide-vue-next'
import { accountingApi } from '@/lib/accounting'
import { localizedAccountName } from '@/lib/coa'
import { isConflict } from '@/lib/api'
import type { Locale } from '@/i18n'
import type { AccountRef } from '@/types/accounting'
import AppButton from '@/components/ui/AppButton.vue'
import AppInput from '@/components/ui/AppInput.vue'
import AppSelect from '@/components/ui/AppSelect.vue'
import AppModal from '@/components/ui/AppModal.vue'
import DataTable from '@/components/ui/DataTable.vue'
import FormField from '@/components/ui/FormField.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import type { Column } from '@/components/ui/types'
import { useAuthStore } from '@/stores/auth'

const { t, locale } = useI18n()
const auth = useAuthStore()
const rows = ref<AccountRef[]>([])
const loading = ref(true)
const modalOpen = ref(false)
const saving = ref(false)
const error = ref('')
const editing = ref<AccountRef | null>(null)
const form = reactive({ code: '', name: '', type: 'Expense', allowPosting: true })

const types = ['Asset', 'Liability', 'Equity', 'Revenue', 'Expense']
const typeOptions = computed(() => types.map((ty) => ({ value: ty, label: t(`accounting.coa.types.${ty}`) })))

const statusFilter = ref<'' | 'active' | 'inactive'>('')
const typeFilter = ref('')
const visibleRows = computed(() =>
  rows.value.filter((r) => {
    if (statusFilter.value === 'active' && !r.isActive) return false
    if (statusFilter.value === 'inactive' && r.isActive) return false
    if (typeFilter.value && r.type !== typeFilter.value) return false
    return true
  }),
)

const columns = computed<Column[]>(() => [
  { key: 'code', label: t('accounting.coa.code') },
  { key: 'name', label: t('accounting.coa.name') },
  { key: 'type', label: t('accounting.coa.type'), hideOnMobile: true },
  { key: 'allowPosting', label: t('accounting.coa.posting'), hideOnMobile: true },
  { key: 'isActive', label: t('accounting.coa.status') },
  { key: 'actions', label: '', align: 'right' },
])

async function load() {
  loading.value = true
  try {
    rows.value = await accountingApi.accounts()
  } finally {
    loading.value = false
  }
}
onMounted(load)

function openNew() {
  editing.value = null
  Object.assign(form, { code: '', name: '', type: 'Expense', allowPosting: true })
  error.value = ''
  modalOpen.value = true
}

function openEdit(row: AccountRef) {
  editing.value = row
  Object.assign(form, { code: row.code, name: row.name, type: row.type, allowPosting: row.allowPosting })
  error.value = ''
  modalOpen.value = true
}

async function save() {
  error.value = ''
  saving.value = true
  try {
    if (editing.value) {
      await accountingApi.updateAccount(editing.value.id, { name: form.name, allowPosting: form.allowPosting }, editing.value.rowVersion)
    } else {
      await accountingApi.createAccount({
        code: form.code,
        name: form.name,
        type: form.type,
        isControlAccount: false,
        controlType: 'None',
      })
    }
    modalOpen.value = false
    await load()
  } catch (e) {
    error.value = isConflict(e)
      ? t('masterData.conflict')
      : (e instanceof Error ? e.message : t('masterData.failed'))
  } finally {
    saving.value = false
  }
}

async function toggleActive(row: AccountRef) {
  error.value = ''
  try {
    await accountingApi.setAccountActive(row.id, !row.isActive)
    await load()
  } catch (e) {
    error.value = e instanceof Error ? e.message : t('masterData.failed')
  }
}
</script>

<template>
  <div class="space-y-4">
    <div class="flex items-center justify-end">
      <AppButton v-if="auth.has('MasterData.Create')" @click="openNew"><Plus :size="16" /> {{ t('accounting.coa.new') }}</AppButton>
    </div>

    <p v-if="error" class="text-sm text-negative">{{ error }}</p>

    <DataTable searchable :columns="columns" :rows="visibleRows" :loading="loading" :empty-text="t('accounting.coa.empty')" :filters-active="!!statusFilter || !!typeFilter" @clear="statusFilter = ''; typeFilter = ''">
      <template #filters>
        <select v-model="typeFilter" class="field-input h-9 text-sm">
          <option value="">{{ t('accounting.coa.allTypes') }}</option>
          <option v-for="ty in types" :key="ty" :value="ty">{{ t(`accounting.coa.types.${ty}`) }}</option>
        </select>
        <select v-model="statusFilter" class="field-input h-9 text-sm">
          <option value="">{{ t('masterData.filters.allStatuses') }}</option>
          <option value="active">{{ t('masterData.active') }}</option>
          <option value="inactive">{{ t('masterData.inactive') }}</option>
        </select>
      </template>
      <template #cell-code="{ row }">
        <span class="tnum text-text-muted">{{ (row as unknown as AccountRef).code }}</span>
      </template>
      <template #cell-name="{ row }">
        <span class="text-text">{{ localizedAccountName(row as unknown as AccountRef, locale as unknown as Locale) }}</span>
        <span v-if="(row as unknown as AccountRef).isControlAccount" class="ml-2 rounded bg-surface-2 px-1.5 py-0.5 text-[10px] uppercase text-text-muted">{{ t('accounting.coa.control') }}</span>
        <span v-if="(row as unknown as AccountRef).isSystem" class="ml-1 rounded bg-surface-2 px-1.5 py-0.5 text-[10px] uppercase text-text-muted">{{ t('accounting.coa.system') }}</span>
      </template>
      <template #cell-type="{ value }">{{ t(`accounting.coa.types.${value}`) }}</template>
      <template #cell-allowPosting="{ value }">
        <Check v-if="value" :size="16" class="text-positive" /><Minus v-else :size="16" class="text-text-muted" />
      </template>
      <template #cell-isActive="{ value }">
        <StatusBadge :label="value ? t('masterData.active') : t('masterData.inactive')" :tone="value ? 'positive' : 'neutral'" />
      </template>
      <template #cell-actions="{ row }">
        <div class="flex items-center justify-end gap-1">
          <button
            v-if="auth.has('MasterData.Edit')"
            class="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium text-text-muted transition-colors hover:bg-surface-2 hover:text-text"
            @click="openEdit(row as unknown as AccountRef)"
          >
            <Pencil :size="14" /> {{ t('masterData.edit') }}
          </button>
          <button
            v-if="auth.has('MasterData.Delete') && !(row as unknown as AccountRef).isSystem"
            class="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium transition-colors hover:bg-surface-2"
            :class="(row as unknown as AccountRef).isActive ? 'text-negative' : 'text-positive'"
            @click="toggleActive(row as unknown as AccountRef)"
          >
            <component :is="(row as unknown as AccountRef).isActive ? Ban : RotateCcw" :size="14" />
            {{ (row as unknown as AccountRef).isActive ? t('masterData.deactivate') : t('masterData.activate') }}
          </button>
        </div>
      </template>
    </DataTable>

    <AppModal v-model="modalOpen" :title="editing ? t('accounting.coa.edit') : t('accounting.coa.new')">
      <div class="space-y-4">
        <div class="grid grid-cols-2 gap-4">
          <FormField :label="t('accounting.coa.code')" required><AppInput v-model="form.code" :disabled="!!editing" /></FormField>
          <FormField :label="t('accounting.coa.type')" required>
            <AppSelect v-model="form.type" :options="typeOptions" :disabled="!!editing" />
          </FormField>
        </div>
        <FormField :label="t('accounting.coa.name')" required><AppInput v-model="form.name" /></FormField>
        <label v-if="editing" class="flex items-center gap-2 text-sm text-text">
          <input v-model="form.allowPosting" type="checkbox" class="h-4 w-4 accent-accent" />
          {{ t('accounting.coa.allowPosting') }}
        </label>
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
