<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { ArrowLeft } from 'lucide-vue-next'
import { salesApi } from '@/lib/sales'
import { masterData, nameMap } from '@/lib/masterData'
import { formatMoney, formatNumber, formatPercent } from '@/lib/format'
import type { SalesOrder } from '@/types/sales'
import AppButton from '@/components/ui/AppButton.vue'
import AppCard from '@/components/ui/AppCard.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()

const order = ref<SalesOrder | null>(null)
const products = ref(new Map<string, string>())
const customers = ref(new Map<string, string>())
const loading = ref(true)
const submitting = ref(false)

const id = computed(() => String(route.params.id))

async function load() {
  loading.value = true
  try {
    const [o, prods, custs] = await Promise.all([
      salesApi.get(id.value),
      masterData.products(),
      masterData.customers(),
    ])
    order.value = o
    products.value = nameMap(prods)
    customers.value = nameMap(custs)
  } finally {
    loading.value = false
  }
}

async function submit() {
  if (!order.value) return
  submitting.value = true
  try {
    await salesApi.submit(order.value.id)
    await load()
  } finally {
    submitting.value = false
  }
}

onMounted(load)
const currency = computed(() => order.value?.currency ?? 'IDR')
</script>

<template>
  <div class="space-y-5">
    <button
      class="inline-flex items-center gap-1.5 text-sm text-text-muted hover:text-text"
      @click="router.push({ name: 'sales' })"
    >
      <ArrowLeft :size="16" /> {{ t('sales.backToList') }}
    </button>

    <p v-if="loading" class="text-sm text-text-muted">{{ t('common.loading') }}</p>

    <template v-else-if="order">
      <!-- Header -->
      <div class="flex flex-wrap items-start justify-between gap-4">
        <div>
          <div class="flex items-center gap-3">
            <h2 class="text-xl font-semibold text-text">{{ order.number }}</h2>
            <StatusBadge :status="order.status" :label="t(`sales.status.${order.status}`)" />
          </div>
          <p class="mt-1 text-sm text-text-muted">
            {{ customers.get(order.customerId) ?? '—' }} · {{ order.orderDate }}
          </p>
        </div>
        <AppButton
          v-if="order.status === 'Draft'"
          :disabled="submitting"
          @click="submit"
        >
          {{ submitting ? t('sales.detail.submitting') : t('sales.detail.submit') }}
        </AppButton>
      </div>

      <!-- Lines -->
      <AppCard :title="t('sales.detail.lines')" :padded="false">
        <div class="overflow-x-auto">
          <table class="w-full text-sm">
            <thead>
              <tr class="border-b border-border text-xs uppercase tracking-wide text-text-muted">
                <th class="px-4 py-2.5 text-left font-semibold">{{ t('sales.detail.product') }}</th>
                <th class="px-4 py-2.5 text-right font-semibold">{{ t('sales.detail.qty') }}</th>
                <th class="px-4 py-2.5 text-right font-semibold">{{ t('sales.detail.unitPrice') }}</th>
                <th class="px-4 py-2.5 text-right font-semibold">{{ t('sales.detail.taxPct') }}</th>
                <th class="px-4 py-2.5 text-right font-semibold">{{ t('sales.detail.delivered') }}</th>
                <th class="px-4 py-2.5 text-right font-semibold">{{ t('sales.detail.lineTotal') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="line in order.lines" :key="line.id" class="border-b border-border last:border-0">
                <td class="px-4 py-3 text-text">{{ products.get(line.productId) ?? '—' }}</td>
                <td class="px-4 py-3 text-right text-text tnum">{{ formatNumber(line.quantity, 0) }}</td>
                <td class="px-4 py-3 text-right text-text tnum">{{ formatMoney(line.unitPrice, currency) }}</td>
                <td class="px-4 py-3 text-right text-text-muted tnum">{{ formatPercent(line.taxRate * 100) }}</td>
                <td class="px-4 py-3 text-right text-text-muted tnum">{{ formatNumber(line.deliveredQuantity, 0) }}</td>
                <td class="px-4 py-3 text-right text-text tnum">{{ formatMoney(line.lineTotal, currency) }}</td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Totals -->
        <div class="flex justify-end border-t border-border p-4">
          <dl class="w-full max-w-xs space-y-1.5 text-sm">
            <div class="flex justify-between">
              <dt class="text-text-muted">{{ t('sales.detail.subtotal') }}</dt>
              <dd class="tnum text-text">{{ formatMoney(order.subTotal, currency) }}</dd>
            </div>
            <div class="flex justify-between">
              <dt class="text-text-muted">{{ t('sales.detail.taxTotal') }}</dt>
              <dd class="tnum text-text">{{ formatMoney(order.taxTotal, currency) }}</dd>
            </div>
            <div class="flex justify-between border-t border-border pt-1.5 font-semibold">
              <dt class="text-text">{{ t('sales.detail.grandTotal') }}</dt>
              <dd class="tnum text-text">{{ formatMoney(order.grandTotal, currency) }}</dd>
            </div>
          </dl>
        </div>
      </AppCard>

      <AppCard v-if="order.notes" :title="t('sales.detail.notes')">
        <p class="text-sm text-text">{{ order.notes }}</p>
      </AppCard>
    </template>
  </div>
</template>
