<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { PackagePlus, Scale } from 'lucide-vue-next'
import { inventoryApi } from '@/lib/inventory'
import { apiErrorMessage } from '@/lib/api'
import { masterData, nameMap } from '@/lib/masterData'
import { exportTable } from '@/lib/exportTable'
import { formatMoney, formatMoneyShort, formatNumber } from '@/lib/format'
import type { StockOnHand } from '@/types/inventory'
import DataTable from '@/components/ui/DataTable.vue'
import ExportMenu from '@/components/ui/ExportMenu.vue'
import InsightCards, { type Insight } from '@/components/ui/InsightCards.vue'
import AppModal from '@/components/ui/AppModal.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppInput from '@/components/ui/AppInput.vue'
import AppSelect from '@/components/ui/AppSelect.vue'
import FormField from '@/components/ui/FormField.vue'
import type { Column, SelectOption } from '@/components/ui/types'
import { useAuthStore } from '@/stores/auth'

const { t } = useI18n()
const router = useRouter()
const auth = useAuthStore()
const canAdjust = computed(() => auth.has('Inventory.Adjust'))
const canTransfer = computed(() => auth.has('Inventory.Transfer'))

const stock = ref<StockOnHand[]>([])
const products = ref(new Map<string, string>())
const warehouses = ref(new Map<string, string>())
const categoryIdByProduct = ref(new Map<string, string | null>())
const categoryNameById = ref(new Map<string, string>())
const loading = ref(true)
const filteredRows = ref<Record<string, unknown>[]>([])
const warehouseFilter = ref('')
const categoryFilter = ref('')
const warehouseOptions = computed(() => [...warehouses.value.entries()].map(([id, name]) => ({ id, name })))
const categoryOptions = computed(() =>
  [...categoryNameById.value.entries()].map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name)),
)

const columns = computed<Column[]>(() => [
  { key: 'product', label: t('inventory.columns.product') },
  { key: 'category', label: t('inventory.columns.category'), hideOnMobile: true },
  { key: 'warehouse', label: t('inventory.columns.warehouse'), hideOnMobile: true },
  { key: 'onHandQty', label: t('inventory.columns.onHand'), align: 'right', numeric: true },
  { key: 'avgUnitCost', label: t('inventory.columns.avgCost'), align: 'right', numeric: true, hideOnMobile: true },
  { key: 'value', label: t('inventory.columns.value'), align: 'right', numeric: true },
  { key: 'actions', label: t('inventory.columns.actions'), align: 'right' },
])

const rows = computed(() =>
  stock.value
    .filter((s) => !warehouseFilter.value || s.warehouseId === warehouseFilter.value)
    .filter((s) => !categoryFilter.value || (categoryIdByProduct.value.get(s.productId) ?? '') === categoryFilter.value)
    .map((s) => {
      const catId = categoryIdByProduct.value.get(s.productId)
      return {
        ...s,
        product: products.value.get(s.productId) ?? '—',
        warehouse: warehouses.value.get(s.warehouseId) ?? '—',
        category: (catId && categoryNameById.value.get(catId)) || '—',
      }
    }),
)

const insights = computed<Insight[]>(() => {
  const inStock = stock.value.filter((s) => s.onHandQty > 0)
  const totalValue = stock.value.reduce((sum, s) => sum + s.value, 0)
  return [
    { label: t('common.insights.items'), value: String(inStock.length) },
    { label: t('common.insights.value'), value: formatMoneyShort(totalValue), tone: 'accent' },
  ]
})

async function reload() {
  stock.value = await inventoryApi.onHand()
}

onMounted(async () => {
  try {
    const [oh, prods, whs, cats] = await Promise.all([
      inventoryApi.onHand(),
      masterData.products(),
      masterData.warehouses(),
      masterData.productCategories(),
    ])
    stock.value = oh
    products.value = nameMap(prods)
    warehouses.value = nameMap(whs)
    categoryIdByProduct.value = new Map(prods.map((p) => [p.id, p.categoryId ?? null]))
    categoryNameById.value = new Map(cats.map((c) => [c.id, c.name]))
  } finally {
    loading.value = false
  }
})

function openCard(row: Record<string, unknown>) {
  router.push({ name: 'inventoryStockCard', query: { productId: String(row.productId), warehouseId: String(row.warehouseId) } })
}

// --- Adjust / opname / transfer modal ---
const today = new Date().toISOString().slice(0, 10)
type Mode = 'adjust' | 'opname' | 'transfer'
type Target = StockOnHand & { product: string; warehouse: string }
const modalOpen = ref(false)
const mode = ref<Mode>('adjust')
const target = ref<Target>()
const saving = ref(false)
const error = ref('')
const message = ref('')

const form = ref({
  increase: false, quantity: 0, unitCost: null as number | null, reason: '',
  counted: 0, notes: '', date: today, toWarehouseId: '',
})

const modalTitle = computed(() => {
  if (!target.value) return ''
  const label =
    mode.value === 'adjust' ? t('inventory.adjust.title')
    : mode.value === 'opname' ? t('inventory.opname.title')
    : t('inventory.transfer.title')
  return `${label} · ${target.value.product}`
})

// Destination warehouses for a transfer — every warehouse except the source (BR: they must differ).
const transferDestinations = computed(() =>
  warehouseOptions.value.filter((w) => w.id !== target.value?.warehouseId),
)

const transferInvalid = computed(() =>
  mode.value === 'transfer' && (!form.value.toWarehouseId || Number(form.value.quantity) <= 0),
)

const submitLabel = computed(() => {
  if (mode.value === 'adjust') return saving.value ? t('inventory.adjust.posting') : t('inventory.adjust.submit')
  if (mode.value === 'opname') return saving.value ? t('inventory.opname.posting') : t('inventory.opname.submit')
  return saving.value ? t('inventory.transfer.posting') : t('inventory.transfer.submit')
})

// A movement dated before today may sit before existing movements; posting it replays the bucket's
// moving average and corrects later costs via an adjusting journal (ADR-0033/0037/0038).
const isBackDated = computed(() => form.value.date < today)

function open(mode_: Mode, row: Record<string, unknown>) {
  const r = row as unknown as Target
  mode.value = mode_
  target.value = r
  error.value = ''
  message.value = ''
  form.value = {
    increase: false, quantity: 0, unitCost: null, reason: '',
    counted: Number(r.onHandQty), notes: '', date: today, toWarehouseId: '',
  }
  modalOpen.value = true
}

async function submit() {
  if (!target.value) return
  saving.value = true
  error.value = ''
  message.value = ''
  try {
    if (mode.value === 'adjust') {
      await inventoryApi.adjust({
        productId: target.value.productId,
        warehouseId: target.value.warehouseId,
        quantity: Number(form.value.quantity),
        increase: form.value.increase,
        unitCost: form.value.increase ? Number(form.value.unitCost ?? 0) : null,
        date: form.value.date,
        reason: form.value.reason,
      })
      await reload()
      modalOpen.value = false
    } else if (mode.value === 'opname') {
      const res = await inventoryApi.opname({
        productId: target.value.productId,
        warehouseId: target.value.warehouseId,
        countedQuantity: Number(form.value.counted),
        unitCost: form.value.unitCost != null ? Number(form.value.unitCost) : null,
        date: form.value.date,
        notes: form.value.notes || null,
      })
      message.value = res.variance === 0
        ? t('inventory.opname.match')
        : t('inventory.opname.variance', { variance: formatNumber(res.variance, 2) })
      await reload()
    } else {
      await inventoryApi.transfer({
        productId: target.value.productId,
        fromWarehouseId: target.value.warehouseId,
        toWarehouseId: form.value.toWarehouseId,
        quantity: Number(form.value.quantity),
        date: form.value.date,
      })
      await reload()
      modalOpen.value = false
    }
  } catch (e) {
    error.value = apiErrorMessage(e, t('inventory.actionFailed'))
  } finally {
    saving.value = false
  }
}

// --- Receive stock (opening balances / manual goods-in) — a standalone form, since it can create a
// brand-new product×warehouse bucket that has no on-hand row yet (unlike the per-row Adjust/Transfer). ---
const productOptions = computed(() =>
  [...products.value.entries()].map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name)),
)

// Searchable-select option arrays ({value,label}) for the entry forms below.
const productSelectOptions = computed<SelectOption[]>(() =>
  productOptions.value.map((p) => ({ value: p.id, label: p.name })),
)
const warehouseSelectOptions = computed<SelectOption[]>(() =>
  warehouseOptions.value.map((w) => ({ value: w.id, label: w.name })),
)
const transferDestinationOptions = computed<SelectOption[]>(() =>
  transferDestinations.value.map((w) => ({ value: w.id, label: w.name })),
)
const receiveOpen = ref(false)
const receiveSaving = ref(false)
const receiveError = ref('')
const receiveForm = ref({ productId: '', warehouseId: '', quantity: 0, unitCost: 0, description: '', date: today })
const receiveInvalid = computed(() =>
  !receiveForm.value.productId || !receiveForm.value.warehouseId || Number(receiveForm.value.quantity) <= 0,
)

function openReceive() {
  receiveError.value = ''
  receiveForm.value = { productId: '', warehouseId: '', quantity: 0, unitCost: 0, description: '', date: today }
  receiveOpen.value = true
}

async function submitReceive() {
  receiveSaving.value = true
  receiveError.value = ''
  try {
    await inventoryApi.receive({
      productId: receiveForm.value.productId,
      warehouseId: receiveForm.value.warehouseId,
      quantity: Number(receiveForm.value.quantity),
      unitCost: Number(receiveForm.value.unitCost),
      date: receiveForm.value.date,
      description: receiveForm.value.description || null,
    })
    await reload()
    receiveOpen.value = false
  } catch (e) {
    receiveError.value = apiErrorMessage(e, t('inventory.actionFailed'))
  } finally {
    receiveSaving.value = false
  }
}
</script>

<template>
  <div class="space-y-4">
    <InsightCards :items="insights" />
    <div class="flex items-center justify-end gap-2">
      <button
        v-if="canAdjust"
        class="inline-flex items-center gap-1.5 rounded-button border border-border px-3 py-2 text-sm font-medium text-text-muted transition-colors hover:bg-surface-2 hover:text-text"
        @click="openReceive"
      >
        <PackagePlus :size="16" /> {{ t('inventory.receive.action') }}
      </button>
      <RouterLink
        :to="{ name: 'inventoryValuation' }"
        class="inline-flex items-center gap-1.5 rounded-button border border-border px-3 py-2 text-sm font-medium text-text-muted transition-colors hover:bg-surface-2 hover:text-text"
      >
        <Scale :size="16" /> {{ t('inventory.valuation.link') }}
      </RouterLink>
      <ExportMenu :download="(f) => exportTable(columns, filteredRows, 'stock-on-hand', f)" />
    </div>
    <DataTable
      v-model:filtered="filteredRows"
      searchable
      :columns="columns"
      :rows="rows"
      :loading="loading"
      :empty-text="t('inventory.empty')"
      clickable
      :filters-active="!!warehouseFilter || !!categoryFilter"
      @row-click="openCard"
      @clear="warehouseFilter = ''; categoryFilter = ''"
    >
      <template #filters>
        <select v-model="warehouseFilter" class="field-input h-9 text-sm">
          <option value="">{{ t('inventory.allWarehouses') }}</option>
          <option v-for="w in warehouseOptions" :key="w.id" :value="w.id">{{ w.name }}</option>
        </select>
        <select v-model="categoryFilter" class="field-input h-9 text-sm">
          <option value="">{{ t('inventory.allCategories') }}</option>
          <option v-for="c in categoryOptions" :key="c.id" :value="c.id">{{ c.name }}</option>
        </select>
      </template>
      <template #cell-onHandQty="{ value }">{{ formatNumber(Number(value), 2) }}</template>
      <template #cell-avgUnitCost="{ value }">{{ formatMoney(Number(value)) }}</template>
      <template #cell-value="{ value }">{{ formatMoney(Number(value)) }}</template>
      <template #cell-actions="{ row }">
        <div class="flex justify-end gap-1" @click.stop>
          <button
            v-if="canAdjust"
            class="rounded-md px-2 py-1 text-xs font-medium text-text-muted transition-colors hover:bg-surface-2 hover:text-text"
            @click="open('adjust', row)"
          >
            {{ t('inventory.adjust.action') }}
          </button>
          <button
            v-if="canAdjust"
            class="rounded-md px-2 py-1 text-xs font-medium text-text-muted transition-colors hover:bg-surface-2 hover:text-text"
            @click="open('opname', row)"
          >
            {{ t('inventory.opname.action') }}
          </button>
          <button
            v-if="canTransfer"
            class="rounded-md px-2 py-1 text-xs font-medium text-text-muted transition-colors hover:bg-surface-2 hover:text-text"
            @click="open('transfer', row)"
          >
            {{ t('inventory.transfer.action') }}
          </button>
          <span v-if="!canAdjust && !canTransfer" class="text-xs text-text-muted">—</span>
        </div>
      </template>
    </DataTable>

    <AppModal
      v-if="target"
      v-model="modalOpen"
      :title="modalTitle"
    >
      <div class="space-y-3">
        <p class="text-sm text-text-muted">{{ target.warehouse }}</p>

        <template v-if="mode === 'adjust'">
          <FormField :label="t('inventory.adjust.direction')">
            <div class="flex gap-2">
              <AppButton :variant="form.increase ? 'primary' : 'secondary'" @click="form.increase = true">{{ t('inventory.adjust.increase') }}</AppButton>
              <AppButton :variant="!form.increase ? 'primary' : 'secondary'" @click="form.increase = false">{{ t('inventory.adjust.decrease') }}</AppButton>
            </div>
          </FormField>
          <FormField :label="t('inventory.adjust.quantity')"><input v-model.number="form.quantity" type="number" min="0" step="any" class="field-input text-right tnum" /></FormField>
          <FormField v-if="form.increase" :label="t('inventory.adjust.unitCost')"><input v-model.number="form.unitCost" type="number" min="0" step="any" class="field-input text-right tnum" /></FormField>
          <FormField :label="t('inventory.adjust.reason')"><AppInput v-model="form.reason" /></FormField>
          <FormField :label="t('inventory.adjust.date')">
            <AppInput v-model="form.date" type="date" />
            <p class="mt-1 text-xs text-text-muted">{{ t('inventory.backdate.hint') }}</p>
            <p v-if="isBackDated" class="mt-1 text-xs text-warning">{{ t('inventory.backdate.warning') }}</p>
          </FormField>
        </template>

        <template v-else-if="mode === 'opname'">
          <FormField :label="t('inventory.opname.systemQty')">
            <p class="tnum text-sm text-text">{{ formatNumber(Number(target.onHandQty), 2) }}</p>
          </FormField>
          <FormField :label="t('inventory.opname.countedQty')"><input v-model.number="form.counted" type="number" min="0" step="any" class="field-input text-right tnum" /></FormField>
          <FormField :label="t('inventory.opname.unitCost')"><input v-model.number="form.unitCost" type="number" min="0" step="any" class="field-input text-right tnum" /></FormField>
          <FormField :label="t('inventory.opname.notes')"><AppInput v-model="form.notes" /></FormField>
          <FormField :label="t('inventory.opname.date')">
            <AppInput v-model="form.date" type="date" />
            <p class="mt-1 text-xs text-text-muted">{{ t('inventory.backdate.hint') }}</p>
            <p v-if="isBackDated" class="mt-1 text-xs text-warning">{{ t('inventory.backdate.warning') }}</p>
          </FormField>
        </template>

        <template v-else>
          <FormField :label="t('inventory.transfer.from')">
            <p class="text-sm text-text">{{ target.warehouse }}</p>
            <p class="mt-1 text-xs text-text-muted">{{ t('inventory.transfer.available', { qty: formatNumber(Number(target.onHandQty), 2) }) }}</p>
          </FormField>
          <FormField :label="t('inventory.transfer.to')">
            <AppSelect
              v-model="form.toWarehouseId"
              :options="transferDestinationOptions"
              :placeholder="t('inventory.transfer.selectWarehouse')"
            />
          </FormField>
          <FormField :label="t('inventory.transfer.quantity')"><input v-model.number="form.quantity" type="number" min="0" step="any" class="field-input text-right tnum" /></FormField>
          <FormField :label="t('inventory.transfer.date')">
            <AppInput v-model="form.date" type="date" />
            <p class="mt-1 text-xs text-text-muted">{{ t('inventory.transfer.hint') }}</p>
            <p v-if="isBackDated" class="mt-1 text-xs text-warning">{{ t('inventory.transfer.backdateWarning') }}</p>
          </FormField>
        </template>

        <p v-if="error" class="text-sm text-negative">{{ error }}</p>
        <p v-if="message" class="text-sm text-positive">{{ message }}</p>
      </div>

      <template #footer>
        <AppButton variant="ghost" @click="modalOpen = false">{{ t('masterData.cancel') }}</AppButton>
        <AppButton :disabled="saving || transferInvalid" @click="submit">
          {{ submitLabel }}
        </AppButton>
      </template>
    </AppModal>

    <AppModal v-model="receiveOpen" :title="t('inventory.receive.title')">
      <div class="space-y-3">
        <p class="text-sm text-text-muted">{{ t('inventory.receive.subtitle') }}</p>
        <FormField :label="t('inventory.receive.product')">
          <AppSelect
            v-model="receiveForm.productId"
            :options="productSelectOptions"
            :placeholder="t('inventory.receive.selectProduct')"
          />
        </FormField>
        <FormField :label="t('inventory.receive.warehouse')">
          <AppSelect
            v-model="receiveForm.warehouseId"
            :options="warehouseSelectOptions"
            :placeholder="t('inventory.receive.selectWarehouse')"
          />
        </FormField>
        <FormField :label="t('inventory.receive.quantity')"><input v-model.number="receiveForm.quantity" type="number" min="0" step="any" class="field-input text-right tnum" /></FormField>
        <FormField :label="t('inventory.receive.unitCost')"><input v-model.number="receiveForm.unitCost" type="number" min="0" step="any" class="field-input text-right tnum" /></FormField>
        <FormField :label="t('inventory.receive.description')"><AppInput v-model="receiveForm.description" /></FormField>
        <FormField :label="t('inventory.receive.date')">
          <AppInput v-model="receiveForm.date" type="date" />
          <p class="mt-1 text-xs text-text-muted">{{ t('inventory.receive.hint') }}</p>
        </FormField>
        <p v-if="receiveError" class="text-sm text-negative">{{ receiveError }}</p>
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="receiveOpen = false">{{ t('masterData.cancel') }}</AppButton>
        <AppButton :disabled="receiveSaving || receiveInvalid" @click="submitReceive">
          {{ receiveSaving ? t('inventory.receive.posting') : t('inventory.receive.submit') }}
        </AppButton>
      </template>
    </AppModal>
  </div>
</template>
