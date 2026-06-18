<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { Check, Minus, Plus } from 'lucide-vue-next'
import { masterData } from '@/lib/masterData'
import type { NamedRef, Product } from '@/types/masterdata'
import AppButton from '@/components/ui/AppButton.vue'
import AppInput from '@/components/ui/AppInput.vue'
import AppModal from '@/components/ui/AppModal.vue'
import AppSelect from '@/components/ui/AppSelect.vue'
import DataTable from '@/components/ui/DataTable.vue'
import FormField from '@/components/ui/FormField.vue'
import type { Column } from '@/components/ui/types'

const { t } = useI18n()
const rows = ref<Product[]>([])
const uoms = ref<NamedRef[]>([])
const categories = ref<NamedRef[]>([])
const loading = ref(true)
const modalOpen = ref(false)
const saving = ref(false)
const error = ref('')
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

async function save() {
  error.value = ''
  saving.value = true
  try {
    await masterData.createProduct({
      code: form.code,
      name: form.name,
      baseUomId: form.baseUomId,
      categoryId: form.categoryId || null,
      isStockTracked: form.isStockTracked,
      isSold: form.isSold,
      isPurchased: form.isPurchased,
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
      <AppButton @click="openNew"><Plus :size="16" /> {{ t('masterData.products.new') }}</AppButton>
    </div>

    <DataTable :columns="columns" :rows="rows" :loading="loading" :empty-text="t('masterData.empty')">
      <template #cell-isStockTracked="{ value }">
        <Check v-if="value" :size="16" class="text-positive" /><Minus v-else :size="16" class="text-text-muted" />
      </template>
      <template #cell-isSold="{ value }">
        <Check v-if="value" :size="16" class="text-positive" /><Minus v-else :size="16" class="text-text-muted" />
      </template>
      <template #cell-isPurchased="{ value }">
        <Check v-if="value" :size="16" class="text-positive" /><Minus v-else :size="16" class="text-text-muted" />
      </template>
    </DataTable>

    <AppModal v-model="modalOpen" :title="t('masterData.products.new')">
      <div class="space-y-4">
        <div class="grid grid-cols-2 gap-4">
          <FormField :label="t('masterData.fields.code')" required><AppInput v-model="form.code" /></FormField>
          <FormField :label="t('masterData.fields.name')" required><AppInput v-model="form.name" /></FormField>
        </div>
        <div class="grid grid-cols-2 gap-4">
          <FormField :label="t('masterData.fields.uom')" required>
            <AppSelect v-model="form.baseUomId" :options="uomOptions" :placeholder="t('masterData.products.selectUom')" />
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
  </div>
</template>
