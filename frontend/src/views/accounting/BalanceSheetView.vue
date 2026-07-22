<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { reportsApi } from '@/lib/reports'
import { localizedAccountName } from '@/lib/coa'
import { downloadFile } from '@/lib/api'
import { formatMoney } from '@/lib/format'
import type { Locale } from '@/i18n'
import type { BalanceSheet } from '@/types/reports'
import AppButton from '@/components/ui/AppButton.vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppInput from '@/components/ui/AppInput.vue'
import FormField from '@/components/ui/FormField.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'

const { t, locale } = useI18n()
const loc = computed(() => locale.value as Locale)
const router = useRouter()
const asOf = ref(new Date().toISOString().slice(0, 10))

// Balance-sheet balances are cumulative, so drill into the ledger up to the as-of date (no start date).
function drill(accountCode: string) {
  router.push({
    name: 'accountingGeneralLedger',
    query: { accountCode, ...(asOf.value ? { toDate: asOf.value } : {}) },
  })
}
const report = ref<BalanceSheet | null>(null)
const loading = ref(true)

async function load() {
  loading.value = true
  try {
    report.value = await reportsApi.balanceSheet(asOf.value || undefined)
  } finally {
    loading.value = false
  }
}
onMounted(load)

function pdf() {
  const q = asOf.value ? `?asOfDate=${asOf.value}` : ''
  downloadFile(`/reports/balance-sheet/pdf${q}`, 'balance-sheet.pdf')
}
</script>

<template>
  <div class="space-y-4">
    <div class="flex flex-wrap items-end gap-3">
      <FormField :label="t('accounting.filters.asOf')"><AppInput v-model="asOf" type="date" /></FormField>
      <AppButton variant="secondary" :disabled="loading" @click="load">{{ t('accounting.filters.apply') }}</AppButton>
      <AppButton variant="ghost" :disabled="loading || !report" @click="pdf">{{ t('accounting.filters.pdf') }}</AppButton>
      <StatusBadge
        v-if="report && !loading"
        class="mb-1"
        :tone="report.isBalanced ? 'positive' : 'negative'"
        :label="report.isBalanced ? t('accounting.bs.balanced') : t('accounting.bs.notBalanced')"
      />
    </div>

    <p v-if="loading" class="text-sm text-text-muted">{{ t('common.loading') }}</p>

    <div v-else-if="report" class="grid grid-cols-1 gap-5 lg:grid-cols-2">
      <!-- Assets -->
      <AppCard :title="t('accounting.bs.assets')" :padded="false">
        <table class="w-full text-sm">
          <tbody>
            <tr v-for="l in report.assets" :key="l.accountCode" class="cursor-pointer border-b border-border transition-colors hover:bg-surface-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-accent" role="button" tabindex="0" :aria-label="t('accounting.drillToLedger') + ': ' + l.accountName" :title="t('accounting.drillToLedger')" @click="drill(l.accountCode)" @keydown.enter="drill(l.accountCode)" @keydown.space.prevent="drill(l.accountCode)">
              <td class="px-4 py-2.5 text-text">{{ localizedAccountName({ code: l.accountCode, name: l.accountName }, loc) }}</td>
              <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(l.amount) }}</td>
            </tr>
          </tbody>
          <tfoot>
            <tr class="border-t-2 border-border font-semibold">
              <td class="px-4 py-3 text-text">{{ t('accounting.bs.totalAssets') }}</td>
              <td class="px-4 py-3 text-right text-text tnum">{{ formatMoney(report.totalAssets) }}</td>
            </tr>
          </tfoot>
        </table>
      </AppCard>

      <!-- Liabilities + Equity -->
      <AppCard :title="`${t('accounting.bs.liabilities')} & ${t('accounting.bs.equity')}`" :padded="false">
        <table class="w-full text-sm">
          <tbody>
            <tr class="bg-surface-2"><td class="px-4 py-2 text-xs font-semibold uppercase tracking-wide text-text-muted" colspan="2">{{ t('accounting.bs.liabilities') }}</td></tr>
            <tr v-for="l in report.liabilities" :key="'l' + l.accountCode" class="cursor-pointer border-b border-border transition-colors hover:bg-surface-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-accent" role="button" tabindex="0" :aria-label="t('accounting.drillToLedger') + ': ' + l.accountName" :title="t('accounting.drillToLedger')" @click="drill(l.accountCode)" @keydown.enter="drill(l.accountCode)" @keydown.space.prevent="drill(l.accountCode)">
              <td class="px-4 py-2.5 text-text">{{ localizedAccountName({ code: l.accountCode, name: l.accountName }, loc) }}</td>
              <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(l.amount) }}</td>
            </tr>
            <tr class="border-b border-border font-medium">
              <td class="px-4 py-2.5 text-text">{{ t('accounting.bs.totalLiabilities') }}</td>
              <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(report.totalLiabilities) }}</td>
            </tr>

            <tr class="bg-surface-2"><td class="px-4 py-2 text-xs font-semibold uppercase tracking-wide text-text-muted" colspan="2">{{ t('accounting.bs.equity') }}</td></tr>
            <tr v-for="l in report.equity" :key="'e' + l.accountCode" class="cursor-pointer border-b border-border transition-colors hover:bg-surface-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-accent" role="button" tabindex="0" :aria-label="t('accounting.drillToLedger') + ': ' + l.accountName" :title="t('accounting.drillToLedger')" @click="drill(l.accountCode)" @keydown.enter="drill(l.accountCode)" @keydown.space.prevent="drill(l.accountCode)">
              <td class="px-4 py-2.5 text-text">{{ localizedAccountName({ code: l.accountCode, name: l.accountName }, loc) }}</td>
              <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(l.amount) }}</td>
            </tr>
            <tr class="border-b border-border">
              <td class="px-4 py-2.5 text-text-muted">{{ t('accounting.bs.currentEarnings') }}</td>
              <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(report.currentEarnings) }}</td>
            </tr>
            <tr class="border-b border-border font-medium">
              <td class="px-4 py-2.5 text-text">{{ t('accounting.bs.totalEquity') }}</td>
              <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(report.totalEquity) }}</td>
            </tr>
          </tbody>
          <tfoot>
            <tr class="border-t-2 border-border font-semibold">
              <td class="px-4 py-3 text-text">{{ t('accounting.bs.totalLiabEquity') }}</td>
              <td class="px-4 py-3 text-right text-text tnum">{{ formatMoney(report.totalLiabilitiesAndEquity) }}</td>
            </tr>
          </tfoot>
        </table>
      </AppCard>
    </div>
  </div>
</template>
