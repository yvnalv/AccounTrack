<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { reportsApi } from '@/lib/reports'
import { downloadFile } from '@/lib/api'
import { formatMoney } from '@/lib/format'
import type { CashFlowStatement } from '@/types/reports'
import AppButton from '@/components/ui/AppButton.vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppInput from '@/components/ui/AppInput.vue'
import FormField from '@/components/ui/FormField.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'

const { t } = useI18n()

const now = new Date()
const monthStart = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-01`
const today = now.toISOString().slice(0, 10)

const fromDate = ref(monthStart)
const toDate = ref(today)
const report = ref<CashFlowStatement | null>(null)
const loading = ref(true)

async function load() {
  loading.value = true
  try {
    report.value = await reportsApi.cashFlow({
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
  downloadFile(`/reports/cash-flow/pdf?${q}`, 'cash-flow.pdf')
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
          <!-- Operating -->
          <tr class="bg-surface-2"><td class="px-4 py-2 text-xs font-semibold uppercase tracking-wide text-text-muted" colspan="2">{{ t('accounting.cf.operating') }}</td></tr>
          <tr class="border-b border-border">
            <td class="px-4 py-2.5 text-text">{{ t('accounting.cf.netIncome') }}</td>
            <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(report.netIncome) }}</td>
          </tr>
          <tr v-for="l in report.operating.lines" :key="'o' + l.accountCode" class="border-b border-border">
            <td class="px-4 py-2.5 pl-8 text-text-muted">{{ l.accountName }}</td>
            <td class="px-4 py-2.5 text-right tnum" :class="l.amount < 0 ? 'text-negative' : 'text-text'">{{ formatMoney(l.amount) }}</td>
          </tr>
          <tr class="border-b border-border font-medium">
            <td class="px-4 py-2.5 text-text">{{ t('accounting.cf.netOperating') }}</td>
            <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(report.operating.total) }}</td>
          </tr>

          <!-- Investing -->
          <tr class="bg-surface-2"><td class="px-4 py-2 text-xs font-semibold uppercase tracking-wide text-text-muted" colspan="2">{{ t('accounting.cf.investing') }}</td></tr>
          <tr v-for="l in report.investing.lines" :key="'i' + l.accountCode" class="border-b border-border">
            <td class="px-4 py-2.5 pl-8 text-text-muted">{{ l.accountName }}</td>
            <td class="px-4 py-2.5 text-right tnum" :class="l.amount < 0 ? 'text-negative' : 'text-text'">{{ formatMoney(l.amount) }}</td>
          </tr>
          <tr v-if="report.investing.lines.length === 0" class="border-b border-border">
            <td class="px-4 py-2.5 pl-8 text-text-muted" colspan="2">{{ t('accounting.cf.empty') }}</td>
          </tr>
          <tr class="border-b border-border font-medium">
            <td class="px-4 py-2.5 text-text">{{ t('accounting.cf.netInvesting') }}</td>
            <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(report.investing.total) }}</td>
          </tr>

          <!-- Financing -->
          <tr class="bg-surface-2"><td class="px-4 py-2 text-xs font-semibold uppercase tracking-wide text-text-muted" colspan="2">{{ t('accounting.cf.financing') }}</td></tr>
          <tr v-for="l in report.financing.lines" :key="'f' + l.accountCode" class="border-b border-border">
            <td class="px-4 py-2.5 pl-8 text-text-muted">{{ l.accountName }}</td>
            <td class="px-4 py-2.5 text-right tnum" :class="l.amount < 0 ? 'text-negative' : 'text-text'">{{ formatMoney(l.amount) }}</td>
          </tr>
          <tr v-if="report.financing.lines.length === 0" class="border-b border-border">
            <td class="px-4 py-2.5 pl-8 text-text-muted" colspan="2">{{ t('accounting.cf.empty') }}</td>
          </tr>
          <tr class="border-b border-border font-medium">
            <td class="px-4 py-2.5 text-text">{{ t('accounting.cf.netFinancing') }}</td>
            <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(report.financing.total) }}</td>
          </tr>

          <!-- Reconciliation -->
          <tr class="border-t-2 border-border text-base font-semibold">
            <td class="px-4 py-3 text-text">{{ t('accounting.cf.netChange') }}</td>
            <td class="px-4 py-3 text-right tnum" :class="report.netChangeInCash < 0 ? 'text-negative' : 'text-positive'">{{ formatMoney(report.netChangeInCash) }}</td>
          </tr>
          <tr class="border-b border-border">
            <td class="px-4 py-2.5 pl-8 text-text-muted">{{ t('accounting.cf.opening') }}</td>
            <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(report.openingCash) }}</td>
          </tr>
          <tr class="font-medium">
            <td class="px-4 py-2.5 text-text">
              {{ t('accounting.cf.closing') }}
              <StatusBadge
                class="ml-2"
                :tone="report.isReconciled ? 'positive' : 'negative'"
                :label="report.isReconciled ? t('accounting.cf.reconciled') : t('accounting.cf.notReconciled')"
              />
            </td>
            <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(report.closingCash) }}</td>
          </tr>
        </tbody>
      </table>
    </AppCard>

    <p class="text-xs text-text-muted">{{ t('accounting.cf.method') }}</p>
  </div>
</template>
