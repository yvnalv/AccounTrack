<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { reportsApi } from '@/lib/reports'
import { downloadFile } from '@/lib/api'
import { formatMoney } from '@/lib/format'
import type { TrialBalance } from '@/types/reports'
import AppButton from '@/components/ui/AppButton.vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppInput from '@/components/ui/AppInput.vue'
import FormField from '@/components/ui/FormField.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'

const { t } = useI18n()
const router = useRouter()
const report = ref<TrialBalance | null>(null)
const loading = ref(true)
const fromDate = ref('')
const toDate = ref('')

// Drill into an account's General Ledger for the same date range.
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

async function load() {
  loading.value = true
  try {
    report.value = await reportsApi.trialBalance({
      fromDate: fromDate.value || undefined,
      toDate: toDate.value || undefined,
    })
  } finally {
    loading.value = false
  }
}
onMounted(load)

const rows = computed(() => report.value?.lines ?? [])

function pdf() {
  const q = new URLSearchParams()
  if (fromDate.value) q.set('fromDate', fromDate.value)
  if (toDate.value) q.set('toDate', toDate.value)
  downloadFile(`/reports/trial-balance/pdf?${q}`, 'trial-balance.pdf')
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

    <AppCard :padded="false">
      <table class="w-full text-sm">
        <thead>
          <tr class="border-b border-border text-xs uppercase tracking-wide text-text-muted">
            <th class="px-4 py-2.5 text-left font-semibold">{{ t('accounting.tb.code') }}</th>
            <th class="px-4 py-2.5 text-left font-semibold">{{ t('accounting.tb.account') }}</th>
            <th class="px-4 py-2.5 text-left font-semibold">{{ t('accounting.tb.type') }}</th>
            <th class="px-4 py-2.5 text-right font-semibold">{{ t('accounting.tb.debit') }}</th>
            <th class="px-4 py-2.5 text-right font-semibold">{{ t('accounting.tb.credit') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="loading"><td colspan="5" class="px-4 py-8 text-center text-text-muted">{{ t('common.loading') }}</td></tr>
          <tr v-else-if="rows.length === 0"><td colspan="5" class="px-4 py-10 text-center text-text-muted">{{ t('accounting.tb.empty') }}</td></tr>
          <tr
            v-for="r in rows"
            v-else
            :key="r.accountCode"
            class="cursor-pointer border-b border-border last:border-0 transition-colors hover:bg-surface-2"
            :title="t('accounting.drillToLedger')"
            @click="drill(r.accountCode)"
          >
            <td class="px-4 py-2.5 text-text-muted tnum">{{ r.accountCode }}</td>
            <td class="px-4 py-2.5 text-text">{{ r.accountName }}</td>
            <td class="px-4 py-2.5 text-text-muted">{{ r.accountType }}</td>
            <td class="px-4 py-2.5 text-right text-text tnum">{{ r.debit ? formatMoney(r.debit) : '' }}</td>
            <td class="px-4 py-2.5 text-right text-text tnum">{{ r.credit ? formatMoney(r.credit) : '' }}</td>
          </tr>
        </tbody>
        <tfoot v-if="!loading && report && rows.length">
          <tr class="border-t-2 border-border font-semibold">
            <td class="px-4 py-3" colspan="3">
              {{ t('accounting.tb.total') }}
              <StatusBadge
                class="ml-2"
                :tone="report.isBalanced ? 'positive' : 'negative'"
                :label="report.isBalanced ? t('accounting.tb.balanced') : t('accounting.tb.notBalanced')"
              />
            </td>
            <td class="px-4 py-3 text-right text-text tnum">{{ formatMoney(report.totalDebit) }}</td>
            <td class="px-4 py-3 text-right text-text tnum">{{ formatMoney(report.totalCredit) }}</td>
          </tr>
        </tfoot>
      </table>
    </AppCard>
  </div>
</template>
