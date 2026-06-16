<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { ArrowLeft, Plus, Trash2 } from 'lucide-vue-next'
import { salesApi } from '@/lib/sales'
import { masterData } from '@/lib/masterData'
import { formatMoney } from '@/lib/format'
import type { NamedRef, Product } from '@/types/masterdata'
import type { CreateSalesOrder } from '@/types/sales'
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

const today = new Date().toISOString().slice(0, 10)
const form = reactive({ customerId: '', warehouseId: '', orderDate: today, notes: '' })
const lines = reactive<LineForm[]>([{ productId: '', quantity: 1, unitPrice: 0, taxPct: 11, description: '' }])

const customers = ref<NamedRef[]>([])
const warehouses = ref<NamedRef[]>([])
const products = ref<Product[]>([])
const error = ref('')
const submitting = ref(false)

onMounted(async () => {
  const [c, w, p] = await Promise.all([
    masterData.customers(),
    masterData.warehouses(),
    masterData.products(),
  ])
  customers.value = c
  warehouses.value = w
  // Active products (the backend doesn't restrict by "is sold"; show all sellable/active stock).
  products.value = p.filter((x) => x.isActive)
})

const customerOptions = computed(() => customers.value.map((c) => ({ value: c.id, label: `${c.code} — ${c.name}` })))
const warehouseOptions = computed(() => warehouses.value.map((w) => ({ value: w.id, label: `${w.code} — ${w.name}` })))
const productOptions = computed(() => products.value.map((p) => ({ value: p.id, label: `${p.code} — ${p.name}` })))

const subTotal = computed(() => lines.reduce((s, l) => s + l.quantity * l.unitPrice, 0))
const taxTotal = computed(() => lines.reduce((s, l) => s + (l.quantity * l.unitPrice * l.taxPct) / 100, 0))
const grandTotal = computed(() => subTotal.value + taxTotal.value)

function addLine() {
  lines.push({ productId: '', quantity: 1, unitPrice: 0, taxPct: 11, description: '' })
}
function removeLine(i: number) {
  lines.splice(i, 1)
}

const validLines = computed(() => lines.filter((l) => l.productId && l.quantity > 0))
const canSubmit = computed(() => !!form.customerId && !!form.warehouseId && validLines.value.length > 0)

async function submit() {
  error.value = ''
  if (!canSubmit.value) {
    error.value = t('sales.form.needLine')
    return
  }
  submitting.value = true
  try {
    const payload: CreateSalesOrder = {
      customerId: form.customerId,
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
    const id = await salesApi.create(payload)
    await router.push({ name: 'salesOrderDetail', params: { id } })
  } catch {
    error.value = t('sales.form.failed')
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="space-y-5">
    <button
      class="inline-flex items-center gap-1.5 text-sm text-text-muted hover:text-text"
      @click="router.push({ name: 'sales' })"
    >
      <ArrowLeft :size="16" /> {{ t('sales.backToList') }}
    </button>

    <AppCard>
      <div class="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <FormField :label="t('sales.form.customer')" required>
          <AppSelect v-model="form.customerId" :options="customerOptions" :placeholder="t('sales.form.selectCustomer')" />
        </FormField>
        <FormField :label="t('sales.form.warehouse')" required>
          <AppSelect v-model="form.warehouseId" :options="warehouseOptions" :placeholder="t('sales.form.selectWarehouse')" />
        </FormField>
        <FormField :label="t('sales.form.orderDate')" required>
          <AppInput v-model="form.orderDate" type="date" />
        </FormField>
      </div>
    </AppCard>

    <AppCard :title="t('sales.detail.lines')" :padded="false">
      <div class="overflow-x-auto">
        <table class="w-full text-sm">
          <thead>
            <tr class="border-b border-border text-xs uppercase tracking-wide text-text-muted">
              <th class="px-4 py-2.5 text-left font-semibold">{{ t('sales.detail.product') }}</th>
              <th class="px-3 py-2.5 text-right font-semibold w-24">{{ t('sales.detail.qty') }}</th>
              <th class="px-3 py-2.5 text-right font-semibold w-36">{{ t('sales.detail.unitPrice') }}</th>
              <th class="px-3 py-2.5 text-right font-semibold w-20">{{ t('sales.detail.taxPct') }}</th>
              <th class="px-4 py-2.5 text-right font-semibold w-36">{{ t('sales.detail.lineTotal') }}</th>
              <th class="w-10"></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(line, i) in lines" :key="i" class="border-b border-border last:border-0">
              <td class="px-4 py-2">
                <select v-model="line.productId" class="field-input">
                  <option value="" disabled>{{ t('sales.form.selectProduct') }}</option>
                  <option v-for="o in productOptions" :key="o.value" :value="o.value">{{ o.label }}</option>
                </select>
              </td>
              <td class="px-3 py-2">
                <input v-model.number="line.quantity" type="number" min="0" step="any" class="field-input text-right tnum" />
              </td>
              <td class="px-3 py-2">
                <input v-model.number="line.unitPrice" type="number" min="0" step="any" class="field-input text-right tnum" />
              </td>
              <td class="px-3 py-2">
                <input v-model.number="line.taxPct" type="number" min="0" max="100" step="any" class="field-input text-right tnum" />
              </td>
              <td class="px-4 py-2 text-right text-text tnum">
                {{ formatMoney(line.quantity * line.unitPrice * (1 + line.taxPct / 100)) }}
              </td>
              <td class="px-2 py-2 text-center">
                <button
                  type="button"
                  class="grid h-8 w-8 place-items-center rounded-control text-text-muted hover:text-negative hover:bg-surface-2"
                  :disabled="lines.length === 1"
                  :title="t('sales.form.remove')"
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
        <AppButton variant="secondary" @click="addLine">
          <Plus :size="16" /> {{ t('sales.form.addLine') }}
        </AppButton>
        <dl class="w-full max-w-xs space-y-1.5 text-sm">
          <div class="flex justify-between">
            <dt class="text-text-muted">{{ t('sales.detail.subtotal') }}</dt>
            <dd class="tnum text-text">{{ formatMoney(subTotal) }}</dd>
          </div>
          <div class="flex justify-between">
            <dt class="text-text-muted">{{ t('sales.detail.taxTotal') }}</dt>
            <dd class="tnum text-text">{{ formatMoney(taxTotal) }}</dd>
          </div>
          <div class="flex justify-between border-t border-border pt-1.5 font-semibold">
            <dt class="text-text">{{ t('sales.detail.grandTotal') }}</dt>
            <dd class="tnum text-text">{{ formatMoney(grandTotal) }}</dd>
          </div>
        </dl>
      </div>
    </AppCard>

    <AppCard>
      <FormField :label="t('sales.form.notes')">
        <AppInput v-model="form.notes" :placeholder="t('sales.form.notes')" />
      </FormField>
    </AppCard>

    <div class="flex items-center justify-end gap-3">
      <p v-if="error" class="text-sm text-negative">{{ error }}</p>
      <AppButton :disabled="!canSubmit || submitting" @click="submit">
        {{ submitting ? t('sales.form.saving') : t('sales.form.save') }}
      </AppButton>
    </div>
  </div>
</template>
