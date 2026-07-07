<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { Plus, Pencil, Tags, Trash2 } from 'lucide-vue-next'
import { masterData, nameMap } from '@/lib/masterData'
import { pricingApi } from '@/lib/pricing'
import { isConflict } from '@/lib/api'
import { formatMoney, formatNumber, formatPercent } from '@/lib/format'
import type { NamedRef, PriceList, PriceListItem, PriceListType } from '@/types/masterdata'
import { useAuthStore } from '@/stores/auth'
import AppButton from '@/components/ui/AppButton.vue'
import AppInput from '@/components/ui/AppInput.vue'
import AppModal from '@/components/ui/AppModal.vue'
import AppSelect from '@/components/ui/AppSelect.vue'
import FormField from '@/components/ui/FormField.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'

const { t } = useI18n()
const auth = useAuthStore()
const canCreate = computed(() => auth.has('MasterData.Create'))
const canEdit = computed(() => auth.has('MasterData.Edit'))

const lists = ref<PriceList[]>([])
const products = ref<NamedRef[]>([])
const productNames = ref(new Map<string, string>())
const loading = ref(true)

const typeOptions = computed(() => [
  { value: 'Sales', label: t('priceLists.type.sales') },
  { value: 'Purchase', label: t('priceLists.type.purchase') },
])
const productOptions = computed(() =>
  products.value.map((p) => ({ value: p.id, label: `${p.code} — ${p.name}` })),
)

async function load() {
  loading.value = true
  try {
    const [l, p] = await Promise.all([pricingApi.list(), masterData.products()])
    lists.value = l
    products.value = p
    productNames.value = nameMap(p)
  } finally {
    loading.value = false
  }
}
onMounted(load)

// --- Create / edit a price list ---
const modalOpen = ref(false)
const saving = ref(false)
const error = ref('')
const editingId = ref<string | null>(null)
const editRowVersion = ref<string | null>(null)
const form = reactive({ name: '', type: 'Sales' as PriceListType, discountPercent: 0, isActive: true })

function openNew() {
  editingId.value = null
  Object.assign(form, { name: '', type: 'Sales' as PriceListType, discountPercent: 0, isActive: true })
  error.value = ''
  modalOpen.value = true
}

function openEdit(row: PriceList) {
  editingId.value = row.id
  editRowVersion.value = row.rowVersion
  Object.assign(form, { name: row.name, type: row.type, discountPercent: row.discountPercent, isActive: row.isActive })
  error.value = ''
  modalOpen.value = true
}

async function save() {
  error.value = ''
  saving.value = true
  try {
    if (editingId.value) {
      await pricingApi.update(
        editingId.value,
        { name: form.name, discountPercent: form.discountPercent, isActive: form.isActive },
        editRowVersion.value,
      )
    } else {
      await pricingApi.create({ name: form.name, type: form.type, discountPercent: form.discountPercent })
    }
    modalOpen.value = false
    await load()
  } catch (e) {
    error.value = isConflict(e) ? t('masterData.conflict') : t('masterData.failed')
  } finally {
    saving.value = false
  }
}

// --- Manage items of a list ---
const itemsOpen = ref(false)
const itemsList = ref<PriceList | null>(null)
const items = ref<PriceListItem[]>([])
const itemsLoading = ref(false)
const itemForm = reactive({ productId: '', unitPrice: 0 })
const itemSaving = ref(false)
const itemError = ref('')

async function openItems(row: PriceList) {
  itemsList.value = row
  itemsOpen.value = true
  itemError.value = ''
  Object.assign(itemForm, { productId: '', unitPrice: 0 })
  await loadItems()
}

async function loadItems() {
  if (!itemsList.value) return
  itemsLoading.value = true
  try {
    items.value = await pricingApi.items(itemsList.value.id)
  } finally {
    itemsLoading.value = false
  }
}

async function addItem() {
  if (!itemsList.value || !itemForm.productId) return
  itemSaving.value = true
  itemError.value = ''
  try {
    await pricingApi.upsertItem(itemsList.value.id, itemForm.productId, itemForm.unitPrice)
    Object.assign(itemForm, { productId: '', unitPrice: 0 })
    await loadItems()
  } catch {
    itemError.value = t('masterData.failed')
  } finally {
    itemSaving.value = false
  }
}

function editItem(item: PriceListItem) {
  Object.assign(itemForm, { productId: item.productId, unitPrice: item.unitPrice })
}

async function removeItem(item: PriceListItem) {
  if (!itemsList.value) return
  await pricingApi.deleteItem(itemsList.value.id, item.productId)
  await loadItems()
}
</script>

<template>
  <div class="space-y-4">
    <div class="flex items-center justify-between">
      <p class="text-sm text-text-muted">{{ t('priceLists.subtitle') }}</p>
      <AppButton v-if="canCreate" @click="openNew"><Plus :size="16" /> {{ t('priceLists.new') }}</AppButton>
    </div>

    <div v-if="loading" class="text-sm text-text-muted">{{ t('common.loading') }}</div>
    <div v-else-if="!lists.length" class="rounded-lg border border-border bg-surface-2 px-4 py-6 text-sm text-text-muted">
      {{ t('priceLists.empty') }}
    </div>

    <div v-else class="overflow-hidden rounded-lg border border-border">
      <table class="w-full text-sm">
        <thead class="bg-surface-2 text-left text-xs text-text-muted">
          <tr>
            <th class="px-3 py-2 font-medium">{{ t('priceLists.name') }}</th>
            <th class="px-3 py-2 font-medium">{{ t('priceLists.typeLabel') }}</th>
            <th class="px-3 py-2 text-right font-medium">{{ t('priceLists.discount') }}</th>
            <th class="px-3 py-2 text-right font-medium">{{ t('priceLists.overridesCount') }}</th>
            <th class="px-3 py-2 font-medium">{{ t('masterData.status') }}</th>
            <th class="px-3 py-2"></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="l in lists" :key="l.id" class="border-t border-border">
            <td class="px-3 py-2">
              <span class="font-medium text-text">{{ l.name }}</span>
            </td>
            <td class="px-3 py-2 text-text-muted">
              {{ l.type === 'Sales' ? t('priceLists.type.sales') : t('priceLists.type.purchase') }}
            </td>
            <td class="px-3 py-2 text-right text-text-muted tnum">
              {{ l.discountPercent > 0 ? formatPercent(l.discountPercent) : '—' }}
            </td>
            <td class="px-3 py-2 text-right text-text-muted tnum">{{ formatNumber(l.itemCount) }}</td>
            <td class="px-3 py-2">
              <StatusBadge :label="l.isActive ? t('masterData.active') : t('masterData.inactive')" :tone="l.isActive ? 'positive' : 'neutral'" />
            </td>
            <td class="px-3 py-2 text-right">
              <div class="flex items-center justify-end gap-1">
                <button class="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium text-text-muted hover:bg-surface-2 hover:text-text" @click="openItems(l)">
                  <Tags :size="14" /> {{ t('priceLists.items') }}
                </button>
                <button v-if="canEdit" class="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium text-text-muted hover:bg-surface-2 hover:text-text" @click="openEdit(l)">
                  <Pencil :size="14" /> {{ t('priceLists.edit') }}
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Create / edit list -->
    <AppModal v-model="modalOpen" :title="editingId ? t('priceLists.edit') : t('priceLists.new')">
      <div class="space-y-4">
        <FormField :label="t('priceLists.name')" required><AppInput v-model="form.name" /></FormField>
        <FormField :label="t('priceLists.typeLabel')">
          <AppSelect v-model="form.type" :options="typeOptions" :disabled="!!editingId" />
        </FormField>
        <FormField :label="t('priceLists.discount')">
          <input v-model.number="form.discountPercent" type="number" min="0" max="100" step="any" class="field-input text-right tnum" />
          <p class="mt-1 text-xs text-text-muted">{{ t('priceLists.discountHint') }}</p>
        </FormField>
        <label v-if="editingId" class="flex items-center gap-2 text-sm text-text">
          <input v-model="form.isActive" type="checkbox" class="h-4 w-4 accent-accent" /> {{ t('priceLists.activeHint') }}
        </label>
        <p v-if="error" class="text-sm text-negative">{{ error }}</p>
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="modalOpen = false">{{ t('masterData.cancel') }}</AppButton>
        <AppButton :disabled="saving || !form.name" @click="save">
          {{ saving ? t('masterData.saving') : t('masterData.save') }}
        </AppButton>
      </template>
    </AppModal>

    <!-- Manage items -->
    <AppModal v-model="itemsOpen" :title="`${t('priceLists.items')} · ${itemsList?.name ?? ''}`">
      <div class="space-y-4">
        <p class="text-xs text-text-muted">{{ t('priceLists.overridesHint') }}</p>
        <div v-if="canEdit" class="flex items-end gap-2">
          <FormField class="flex-1" :label="t('priceLists.product')">
            <AppSelect v-model="itemForm.productId" :options="productOptions" :placeholder="t('priceLists.selectProduct')" />
          </FormField>
          <FormField :label="t('priceLists.price')">
            <input v-model.number="itemForm.unitPrice" type="number" min="0" step="any" class="field-input w-36 text-right tnum" />
          </FormField>
          <AppButton :disabled="itemSaving || !itemForm.productId" @click="addItem">{{ t('priceLists.addItem') }}</AppButton>
        </div>
        <p v-if="itemError" class="text-sm text-negative">{{ itemError }}</p>

        <div v-if="itemsLoading" class="text-sm text-text-muted">{{ t('common.loading') }}</div>
        <p v-else-if="!items.length" class="text-sm text-text-muted">{{ t('priceLists.noItems') }}</p>
        <table v-else class="w-full text-sm">
          <thead class="text-left text-xs text-text-muted">
            <tr>
              <th class="py-1.5 font-medium">{{ t('priceLists.product') }}</th>
              <th class="py-1.5 text-right font-medium">{{ t('priceLists.price') }}</th>
              <th class="py-1.5"></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in items" :key="item.id" class="border-t border-border">
              <td class="py-1.5 text-text">{{ productNames.get(item.productId) ?? '—' }}</td>
              <td class="py-1.5 text-right text-text tnum">{{ formatMoney(item.unitPrice) }}</td>
              <td class="py-1.5 text-right">
                <div v-if="canEdit" class="flex items-center justify-end gap-1">
                  <button class="rounded-md px-2 py-1 text-xs text-text-muted hover:bg-surface-2 hover:text-text" @click="editItem(item)">
                    <Pencil :size="13" />
                  </button>
                  <button class="rounded-md px-2 py-1 text-xs text-negative hover:bg-surface-2" @click="removeItem(item)">
                    <Trash2 :size="13" />
                  </button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="itemsOpen = false">{{ t('priceLists.close') }}</AppButton>
      </template>
    </AppModal>
  </div>
</template>
