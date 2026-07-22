<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { reportsApi } from '@/lib/reports'
import { accountingApi } from '@/lib/accounting'
import { accountOptionLabel, localizedAccountName } from '@/lib/coa'
import { downloadFile } from '@/lib/api'
import { formatMoney } from '@/lib/format'
import type { Locale } from '@/i18n'
import type { GeneralLedger } from '@/types/reports'
import type { AccountRef } from '@/types/accounting'
import type { SelectOption } from '@/components/ui/types'
import AppButton from '@/components/ui/AppButton.vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppInput from '@/components/ui/AppInput.vue'
import AppSelect from '@/components/ui/AppSelect.vue'
import FormField from '@/components/ui/FormField.vue'

const { t, locale } = useI18n()
const loc = computed(() => locale.value as Locale)
const route = useRoute()

// Format dates from the LOCAL calendar (never mix local + toISOString/UTC, which can invert the
// default range near midnight / month boundaries and make the ledger look empty).
const now = new Date()
const pad = (n: number) => String(n).padStart(2, '0')
const isoDate = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
const monthStart = isoDate(new Date(now.getFullYear(), now.getMonth(), 1))
const today = isoDate(now)

const fromDate = ref(monthStart)
const toDate = ref(today)
const accountId = ref('')
const report = ref<GeneralLedger | null>(null)
const accounts = ref<AccountRef[]>([])
const loading = ref(true)

// A ledger must let you drill into ANY account — including control accounts (AR/AP/Inventory) that
// are not directly postable — so we do NOT filter by allowPosting here.
const accountOptions = computed<SelectOption[]>(() => [
  { value: '', label: t('accounting.gl.allAccounts') },
  ...accounts.value.map((a) => ({ value: a.id, label: accountOptionLabel(a, loc.value) })),
])

async function load() {
  loading.value = true
  try {
    report.value = await reportsApi.generalLedger({
      accountId: accountId.value || undefined,
      fromDate: fromDate.value || undefined,
      toDate: toDate.value || undefined,
    })
  } finally {
    loading.value = false
  }
}

// Apply immediately when the account is changed (dates keep the explicit "Apply" button, so typing a
// date doesn't fire a request on every keystroke). The watch runs after accountId updates, avoiding
// the @change/v-model ordering race. Suppressed while applying query params on mount so the initial
// drill-down (from a report) loads exactly once.
let suppressWatch = true
watch(accountId, () => {
  if (!suppressWatch) load()
})

onMounted(async () => {
  accounts.value = await accountingApi.accounts()

  // Drill-down from a report: pre-select the account (by id, or by code resolved against the chart) and
  // carry the report's date range, so clicking an account row lands on its ledger for the same period.
  const q = route.query
  if (typeof q.fromDate === 'string') fromDate.value = q.fromDate
  if (typeof q.toDate === 'string') toDate.value = q.toDate
  if (typeof q.accountId === 'string') accountId.value = q.accountId
  else if (typeof q.accountCode === 'string') {
    accountId.value = accounts.value.find((a) => a.code === q.accountCode)?.id ?? ''
  }

  await load()
  suppressWatch = false
})

function pdf() {
  const q = new URLSearchParams()
  if (accountId.value) q.set('accountId', accountId.value)
  if (fromDate.value) q.set('fromDate', fromDate.value)
  if (toDate.value) q.set('toDate', toDate.value)
  downloadFile(`/reports/general-ledger/pdf?${q}`, 'general-ledger.pdf')
}
</script>

<template>
  <div class="space-y-4">
    <div class="flex flex-wrap items-end gap-3">
      <FormField :label="t('accounting.gl.account')" class="min-w-56">
        <AppSelect v-model="accountId" :options="accountOptions" />
      </FormField>
      <FormField :label="t('accounting.filters.from')"><AppInput v-model="fromDate" type="date" /></FormField>
      <FormField :label="t('accounting.filters.to')"><AppInput v-model="toDate" type="date" /></FormField>
      <AppButton variant="secondary" :disabled="loading" @click="load">{{ t('accounting.filters.apply') }}</AppButton>
      <AppButton variant="ghost" :disabled="loading || !report" @click="pdf">{{ t('accounting.filters.pdf') }}</AppButton>
    </div>

    <p v-if="loading" class="text-sm text-text-muted">{{ t('common.loading') }}</p>

    <p v-else-if="!report || report.accounts.length === 0" class="px-4 py-10 text-center text-sm text-text-muted">
      {{ t('accounting.gl.empty') }}
    </p>

    <AppCard v-for="acc in report?.accounts ?? []" v-else :key="acc.accountId" :padded="false">
      <div class="flex items-center justify-between border-b border-border px-4 py-2.5">
        <h3 class="text-sm font-semibold text-text">{{ acc.accountCode }} · {{ localizedAccountName({ code: acc.accountCode, name: acc.accountName }, loc) }}</h3>
        <span class="text-xs text-text-muted">{{ t(`accounting.coa.types.${acc.accountType}`) }}</span>
      </div>
      <table class="w-full text-sm">
        <thead>
          <tr class="border-b border-border text-xs uppercase tracking-wide text-text-muted">
            <th class="px-4 py-2 text-left font-semibold">{{ t('accounting.gl.date') }}</th>
            <th class="px-3 py-2 text-left font-semibold">{{ t('accounting.gl.entry') }}</th>
            <th class="px-3 py-2 text-left font-semibold">{{ t('accounting.gl.description') }}</th>
            <th class="px-3 py-2 text-right font-semibold">{{ t('accounting.gl.debit') }}</th>
            <th class="px-3 py-2 text-right font-semibold">{{ t('accounting.gl.credit') }}</th>
            <th class="px-4 py-2 text-right font-semibold">{{ t('accounting.gl.balance') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr class="border-b border-border text-text-muted">
            <td class="px-4 py-2" colspan="5">{{ t('accounting.gl.opening') }}</td>
            <td class="px-4 py-2 text-right tnum">{{ formatMoney(acc.openingBalance) }}</td>
          </tr>
          <tr v-for="(e, i) in acc.entries" :key="i" class="border-b border-border">
            <td class="px-4 py-2 text-text-muted tnum">{{ e.date }}</td>
            <td class="px-3 py-2 text-text-muted">{{ e.entryNo }} · {{ t(`accounting.sources.${e.source}`) }}</td>
            <td class="px-3 py-2 text-text">{{ e.description ?? '—' }}</td>
            <td class="px-3 py-2 text-right text-text tnum">{{ e.debit ? formatMoney(e.debit) : '' }}</td>
            <td class="px-3 py-2 text-right text-text tnum">{{ e.credit ? formatMoney(e.credit) : '' }}</td>
            <td class="px-4 py-2 text-right tnum" :class="e.runningBalance < 0 ? 'text-negative' : 'text-text'">{{ formatMoney(e.runningBalance) }}</td>
          </tr>
        </tbody>
        <tfoot>
          <tr class="border-t-2 border-border font-semibold">
            <td class="px-4 py-2.5" colspan="3">{{ t('accounting.gl.closing') }}</td>
            <td class="px-3 py-2.5 text-right text-text tnum">{{ formatMoney(acc.totalDebit) }}</td>
            <td class="px-3 py-2.5 text-right text-text tnum">{{ formatMoney(acc.totalCredit) }}</td>
            <td class="px-4 py-2.5 text-right tnum" :class="acc.closingBalance < 0 ? 'text-negative' : 'text-text'">{{ formatMoney(acc.closingBalance) }}</td>
          </tr>
        </tfoot>
      </table>
    </AppCard>
  </div>
</template>
