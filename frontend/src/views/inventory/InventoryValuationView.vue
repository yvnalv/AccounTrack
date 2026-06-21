<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { ArrowLeft, FileText } from 'lucide-vue-next'
import { inventoryApi } from '@/lib/inventory'
import { downloadFile } from '@/lib/api'
import { formatMoney, formatNumber } from '@/lib/format'
import type { InventoryValuation } from '@/types/inventory'
import AppButton from '@/components/ui/AppButton.vue'
import AppCard from '@/components/ui/AppCard.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'

const { t } = useI18n()
const report = ref<InventoryValuation | null>(null)
const loading = ref(true)

onMounted(async () => {
  try {
    report.value = await inventoryApi.valuation()
  } finally {
    loading.value = false
  }
})

const pdf = () => downloadFile('/stock/valuation/pdf', 'inventory-valuation.pdf')
</script>

<template>
  <div class="space-y-4">
    <div class="flex items-center justify-between">
      <RouterLink
        :to="{ name: 'inventory' }"
        class="inline-flex items-center gap-1 text-sm text-text-muted transition-colors hover:text-text"
      >
        <ArrowLeft :size="16" /> {{ t('inventory.valuation.back') }}
      </RouterLink>
      <AppButton variant="ghost" :disabled="loading || !report" @click="pdf">
        <FileText :size="16" /> {{ t('accounting.filters.pdf') }}
      </AppButton>
    </div>

    <p v-if="loading" class="text-sm text-text-muted">{{ t('common.loading') }}</p>

    <AppCard v-else-if="report" :padded="false">
      <table class="w-full text-sm">
        <thead>
          <tr class="border-b border-border text-xs uppercase tracking-wide text-text-muted">
            <th class="px-4 py-2.5 text-left font-semibold">{{ t('inventory.valuation.product') }}</th>
            <th class="px-3 py-2.5 text-right font-semibold">{{ t('inventory.valuation.qty') }}</th>
            <th class="px-3 py-2.5 text-right font-semibold">{{ t('inventory.valuation.avgCost') }}</th>
            <th class="px-4 py-2.5 text-right font-semibold">{{ t('inventory.valuation.value') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="report.rows.length === 0">
            <td colspan="4" class="px-4 py-10 text-center text-text-muted">{{ t('inventory.valuation.empty') }}</td>
          </tr>
          <tr v-for="r in report.rows" v-else :key="r.productId" class="border-b border-border last:border-0">
            <td class="px-4 py-2.5 text-text">{{ r.productName }}</td>
            <td class="px-3 py-2.5 text-right text-text tnum">{{ formatNumber(r.quantity, 2) }}</td>
            <td class="px-3 py-2.5 text-right text-text-muted tnum">{{ formatMoney(r.avgUnitCost, report.currency) }}</td>
            <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(r.value, report.currency) }}</td>
          </tr>
        </tbody>
        <tfoot>
          <tr class="border-t-2 border-border text-base font-semibold">
            <td class="px-4 py-3 text-text" colspan="3">{{ t('inventory.valuation.total') }}</td>
            <td class="px-4 py-3 text-right text-text tnum">{{ formatMoney(report.totalValue, report.currency) }}</td>
          </tr>
          <tr class="border-b border-border text-text-muted">
            <td class="px-4 py-2 pl-8" colspan="3">{{ t('inventory.valuation.glBalance') }}</td>
            <td class="px-4 py-2 text-right tnum">{{ formatMoney(report.glInventoryBalance, report.currency) }}</td>
          </tr>
          <tr class="font-medium">
            <td class="px-4 py-2.5" colspan="3">
              {{ t('inventory.valuation.difference') }}
              <StatusBadge
                class="ml-2"
                :tone="report.isReconciled ? 'positive' : 'negative'"
                :label="report.isReconciled ? t('inventory.valuation.reconciled') : t('inventory.valuation.notReconciled')"
              />
            </td>
            <td class="px-4 py-2.5 text-right tnum" :class="report.isReconciled ? 'text-text' : 'text-negative'">
              {{ formatMoney(report.difference, report.currency) }}
            </td>
          </tr>
        </tfoot>
      </table>
    </AppCard>

    <p class="text-xs text-text-muted">{{ t('inventory.valuation.note') }}</p>
  </div>
</template>
