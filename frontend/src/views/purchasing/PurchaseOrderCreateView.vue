<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { ArrowLeft, Plus, Trash2 } from 'lucide-vue-next'
import { purchasingApi } from '@/lib/purchasing'
import { masterData } from '@/lib/masterData'
import { pricingApi } from '@/lib/pricing'
import { useCompanyStore } from '@/stores/company'
import { formatMoney } from '@/lib/format'
import type { NamedRef, Product } from '@/types/masterdata'
import type { CreatePurchaseOrder } from '@/types/purchasing'
import AppButton from '@/components/ui/AppButton.vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppInput from '@/components/ui/AppInput.vue'
import AppSelect from '@/components/ui/AppSelect.vue'
import FormField from '@/components/ui/FormField.vue'

interface LineForm {
  productId: string
  quantity: number
  unitPrice: number
  taxPct: number
  description: string
}

const { t } = useI18n()
const router = useRouter()
const route = useRoute()
const company = useCompanyStore()

const editId = route.query.edit ? String(route.query.edit) : null
const defaultTaxPct = computed(() => (company.vatRegistered ? 11 : 0))

const today = new Date().toISOString().slice(0, 10)
const form = reactive({ supplierId: '', warehouseId: '', orderDate: today, notes: '' })
const lines = reactive<LineForm[]>([{ productId: '', quantity: 1, unitPrice: 0, taxPct: 0, description: '' }])

const suppliers = ref<NamedRef[]>([])
const warehouses = ref<NamedRef[]>([])
const products = ref<Product[]>([])
const error = ref('')
const submitting = ref(false)
// Concurrency token captured when editing, echoed back on save to detect a stale edit (ADR-0021).
const editRowVersion = ref<string | null>(null)

onMounted(async () => {
  const [s, w, p] = await Promise.all([
    masterData.suppliers(),
    masterData.warehouses(),
    masterData.products(),
    company.ensure(),
  ])
  suppliers.value = s
  warehouses.value = w
  products.value = p.filter((x) => x.isActive)

  if (editId) {
    const o = await purchasingApi.get(editId)
    editRowVersion.value = o.rowVersion
    form.supplierId = o.supplierId
    form.warehouseId = o.warehouseId
    form.orderDate = o.orderDate
    form.notes = o.notes ?? ''
    lines.splice(0, lines.length, ...o.lines.map((l) => ({
      productId: l.productId,
      quantity: l.quantity,
      unitPrice: l.unitPrice,
      taxPct: l.taxRate * 100,
      description: l.description ?? '',
    })))
  } else {
    lines.forEach((l) => (l.taxPct = defaultTaxPct.value))
  }
})

const supplierOptions = computed(() => suppliers.value.map((s) => ({ value: s.id, label: `${s.code} — ${s.name}` })))
const warehouseOptions = computed(() => warehouses.value.map((w) => ({ value: w.id, label: `${w.code} — ${w.name}` })))
const productOptions = computed(() => products.value.map((p) => ({ value: p.id, label: `${p.code} — ${p.name}` })))

const subTotal = computed(() => lines.reduce((s, l) => s + l.quantity * l.unitPrice, 0))
const taxTotal = computed(() => lines.reduce((s, l) => s + (l.quantity * l.unitPrice * l.taxPct) / 100, 0))
const grandTotal = computed(() => subTotal.value + taxTotal.value)

const addLine = () => lines.push({ productId: '', quantity: 1, unitPrice: 0, taxPct: defaultTaxPct.value, description: '' })
const removeLine = (i: number) => lines.splice(i, 1)

// Price auto-fill (ADR-0035): the product's base purchase price is the default; the chosen supplier's
// price list may override or discount it. Prefills each line on product-select (still editable).
const priceMap = ref<Record<string, number>>({})
function basePrice(productId: string): number | null {
  return products.value.find((p) => p.id === productId)?.purchasePrice ?? null
}
function applyPrice(line: LineForm) {
  const price = priceMap.value[line.productId] ?? basePrice(line.productId)
  if (price != null) line.unitPrice = price
}
async function loadPrices() {
  priceMap.value = form.supplierId ? await pricingApi.resolve('Purchase', form.supplierId) : {}
  lines.forEach((l) => { if (l.productId && !l.unitPrice) applyPrice(l) })
}
watch(() => form.supplierId, loadPrices)

const validLines = computed(() => lines.filter((l) => l.productId && l.quantity > 0))
const canSubmit = computed(() => !!form.supplierId && !!form.warehouseId && validLines.value.length > 0)

async function submit() {
  error.value = ''
  if (!canSubmit.value) {
    error.value = t('purchasing.form.needLine')
    return
  }
  submitting.value = true
  try {
    const payload: CreatePurchaseOrder = {
      supplierId: form.supplierId,
      warehouseId: form.warehouseId,
      orderDate: form.orderDate,
      notes: form.notes || null,
      lines: validLines.value.map((l) => ({
        productId: l.productId,
        quantity: l.quantity,
        unitPrice: l.unitPrice,
        taxRate: l.taxPct / 100,
        description: l.description || null,
      })),
    }
    const id = editId
      ? await purchasingApi.update(editId, payload, editRowVersion.value)
      : await purchasingApi.create(payload)
    await router.push({ name: 'purchaseOrderDetail', params: { id } })
  } catch (e) {
    error.value =
      (e as { response?: { status?: number } })?.response?.status === 409
        ? t('purchasing.form.conflict')
        : t('purchasing.form.failed')
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="space-y-5">
    <button
      class="inline-flex items-center gap-1.5 text-sm text-text-muted hover:text-text"
      @click="router.push({ name: 'purchasing' })"
    >
      <ArrowLeft :size="16" /> {{ t('purchasing.backToList') }}
    </button>

    <AppCard>
      <div class="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <FormField :label="t('purchasing.form.supplier')" required>
          <AppSelect v-model="form.supplierId" :options="supplierOptions" :placeholder="t('purchasing.form.selectSupplier')" />
        </FormField>
        <FormField :label="t('purchasing.form.warehouse')" required>
          <AppSelect v-model="form.warehouseId" :options="warehouseOptions" :placeholder="t('purchasing.form.selectWarehouse')" />
        </FormField>
        <FormField :label="t('purchasing.form.orderDate')" required>
          <AppInput v-model="form.orderDate" type="date" />
        </FormField>
      </div>
    </AppCard>

    <AppCard :title="t('purchasing.detail.lines')" :padded="false">
      <div class="overflow-x-auto">
        <table class="w-full text-sm">
          <thead>
            <tr class="border-b border-border text-xs uppercase tracking-wide text-text-muted">
              <th class="px-4 py-2.5 text-left font-semibold">{{ t('purchasing.detail.product') }}</th>
              <th class="px-3 py-2.5 text-right font-semibold w-24">{{ t('purchasing.detail.ordered') }}</th>
              <th class="px-3 py-2.5 text-right font-semibold w-36">{{ t('purchasing.detail.unitPrice') }}</th>
              <th v-if="company.vatRegistered" class="px-3 py-2.5 text-right font-semibold w-20">{{ t('purchasing.detail.taxPct') }}</th>
              <th class="px-4 py-2.5 text-right font-semibold w-36">{{ t('purchasing.detail.lineTotal') }}</th>
              <th class="w-10"></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(line, i) in lines" :key="i" class="border-b border-border last:border-0">
              <td class="px-4 py-2">
                <select v-model="line.productId" class="field-input" @change="applyPrice(line)">
                  <option value="" disabled>{{ t('purchasing.form.selectProduct') }}</option>
                  <option v-for="o in productOptions" :key="o.value" :value="o.value">{{ o.label }}</option>
                </select>
              </td>
              <td class="px-3 py-2"><input v-model.number="line.quantity" type="number" min="0" step="any" class="field-input text-right tnum" /></td>
              <td class="px-3 py-2"><input v-model.number="line.unitPrice" type="number" min="0" step="any" class="field-input text-right tnum" /></td>
              <td v-if="company.vatRegistered" class="px-3 py-2"><input v-model.number="line.taxPct" type="number" min="0" max="100" step="any" class="field-input text-right tnum" /></td>
              <td class="px-4 py-2 text-right text-text tnum">{{ formatMoney(line.quantity * line.unitPrice * (1 + line.taxPct / 100)) }}</td>
              <td class="px-2 py-2 text-center">
                <button
                  type="button"
                  class="grid h-8 w-8 place-items-center rounded-control text-text-muted hover:text-negative hover:bg-surface-2"
                  :disabled="lines.length === 1"
                  :title="t('purchasing.form.remove')"
                  @click="removeLine(i)"
                >
                  <Trash2 :size="16" />
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      <div class="flex items-center justify-between border-t border-border p-4">
        <AppButton variant="secondary" @click="addLine"><Plus :size="16" /> {{ t('purchasing.form.addLine') }}</AppButton>
        <dl class="w-full max-w-xs space-y-1.5 text-sm">
          <div class="flex justify-between"><dt class="text-text-muted">{{ t('purchasing.detail.subtotal') }}</dt><dd class="tnum text-text">{{ formatMoney(subTotal) }}</dd></div>
          <div v-if="company.vatRegistered" class="flex justify-between"><dt class="text-text-muted">{{ t('purchasing.detail.taxTotal') }}</dt><dd class="tnum text-text">{{ formatMoney(taxTotal) }}</dd></div>
          <div class="flex justify-between border-t border-border pt-1.5 font-semibold"><dt class="text-text">{{ t('purchasing.detail.grandTotal') }}</dt><dd class="tnum text-text">{{ formatMoney(grandTotal) }}</dd></div>
        </dl>
      </div>
    </AppCard>

    <AppCard>
      <FormField :label="t('purchasing.form.notes')">
        <AppInput v-model="form.notes" :placeholder="t('purchasing.form.notes')" />
      </FormField>
    </AppCard>

    <div class="flex items-center justify-end gap-3">
      <p v-if="error" class="text-sm text-negative">{{ error }}</p>
      <AppButton :disabled="!canSubmit || submitting" @click="submit">
        {{ submitting ? t('purchasing.form.saving') : (editId ? t('purchasing.form.update') : t('purchasing.form.save')) }}
      </AppButton>
    </div>
  </div>
</template>
