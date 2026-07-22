<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { reportsApi } from '@/lib/reports'
import { localizedAccountName } from '@/lib/coa'
import { downloadFile } from '@/lib/api'
import { formatMoney } from '@/lib/format'
import type { Locale } from '@/i18n'
import type { ProfitAndLoss } from '@/types/reports'
import AppButton from '@/components/ui/AppButton.vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppInput from '@/components/ui/AppInput.vue'
import FormField from '@/components/ui/FormField.vue'

const { t, locale } = useI18n()
const loc = computed(() => locale.value as Locale)
const router = useRouter()

function drill(accountCode: string) {
  router.push({
    name: 'accountingGeneralLedger',
    query: {
      accountCode,
      ...(fromDate.value ? { fromDate: fromDate.value } : {}),
      ...(toDate.value ? { toDate: toDate.value } : {}),
    },
  })
}

const now = new Date()
const monthStart = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-01`
const today = now.toISOString().slice(0, 10)

const fromDate = ref(monthStart)
const toDate = ref(today)
const report = ref<ProfitAndLoss | null>(null)
const loading = ref(true)

async function load() {
  loading.value = true
  try {
    report.value = await reportsApi.profitLoss({
      fromDate: fromDate.value || undefined,
      toDate: toDate.value || undefined,
    })
  } finally {
    loading.value = false
  }
}
onMounted(load)

function pdf() {
  const q = new URLSearchParams()
  if (fromDate.value) q.set('fromDate', fromDate.value)
  if (toDate.value) q.set('toDate', toDate.value)
  downloadFile(`/reports/profit-loss/pdf?${q}`, 'profit-loss.pdf')
}
</script>

<template>
  <div class="space-y-4">
    <div class="flex flex-wrap items-end gap-3">
      <FormField :label="t('accounting.filters.from')"><AppInput v-model="fromDate" type="date" /></FormField>
      <FormField :label="t('accounting.filters.to')"><AppInput v-model="toDate" type="date" /></FormField>
      <AppButton variant="secondary" :disabled="loading" @click="load">{{ t('accounting.filters.apply') }}</AppButton>
      <AppButton variant="ghost" :disabled="loading || !report" @click="pdf">{{ t('accounting.filters.pdf') }}</AppButton>
    </div>

    <p v-if="loading" class="text-sm text-text-muted">{{ t('common.loading') }}</p>

    <AppCard v-else-if="report" :padded="false">
      <table class="w-full text-sm">
        <tbody>
          <!-- Revenue -->
          <tr class="bg-surface-2"><td class="px-4 py-2 text-xs font-semibold uppercase tracking-wide text-text-muted" colspan="2">{{ t('accounting.pl.revenue') }}</td></tr>
          <tr v-for="l in report.revenue" :key="'r' + l.accountCode" class="cursor-pointer border-b border-border transition-colors hover:bg-surface-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-accent" role="button" tabindex="0" :aria-label="t('accounting.drillToLedger') + ': ' + l.accountName" :title="t('accounting.drillToLedger')" @click="drill(l.accountCode)" @keydown.enter="drill(l.accountCode)" @keydown.space.prevent="drill(l.accountCode)">
            <td class="px-4 py-2.5 text-text">{{ localizedAccountName({ code: l.accountCode, name: l.accountName }, loc) }}</td>
            <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(l.amount) }}</td>
          </tr>
          <tr class="border-b border-border font-medium">
            <td class="px-4 py-2.5 text-text">{{ t('accounting.pl.totalRevenue') }}</td>
            <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(report.totalRevenue) }}</td>
          </tr>

          <!-- Expenses -->
          <tr class="bg-surface-2"><td class="px-4 py-2 text-xs font-semibold uppercase tracking-wide text-text-muted" colspan="2">{{ t('accounting.pl.expenses') }}</td></tr>
          <tr v-for="l in report.expenses" :key="'e' + l.accountCode" class="cursor-pointer border-b border-border transition-colors hover:bg-surface-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-accent" role="button" tabindex="0" :aria-label="t('accounting.drillToLedger') + ': ' + l.accountName" :title="t('accounting.drillToLedger')" @click="drill(l.accountCode)" @keydown.enter="drill(l.accountCode)" @keydown.space.prevent="drill(l.accountCode)">
            <td class="px-4 py-2.5 text-text">{{ localizedAccountName({ code: l.accountCode, name: l.accountName }, loc) }}</td>
            <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(l.amount) }}</td>
          </tr>
          <tr class="border-b border-border font-medium">
            <td class="px-4 py-2.5 text-text">{{ t('accounting.pl.totalExpenses') }}</td>
            <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(report.totalExpenses) }}</td>
          </tr>
        </tbody>
        <tfoot>
          <tr class="border-t-2 border-border text-base font-semibold">
            <td class="px-4 py-3 text-text">{{ report.netProfit >= 0 ? t('accounting.pl.netProfit') : t('accounting.pl.netLoss') }}</td>
            <td class="px-4 py-3 text-right tnum" :class="report.netProfit >= 0 ? 'text-positive' : 'text-negative'">
              {{ formatMoney(report.netProfit) }}
            </td>
          </tr>
        </tfoot>
      </table>
    </AppCard>
  </div>
</template>
