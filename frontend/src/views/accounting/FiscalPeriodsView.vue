<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { accountingApi } from '@/lib/accounting'
import { formatMoney } from '@/lib/format'
import type { FiscalYear } from '@/types/accounting'
import AppButton from '@/components/ui/AppButton.vue'
import AppCard from '@/components/ui/AppCard.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'

const { t } = useI18n()

const years = ref<FiscalYear[]>([])
const loading = ref(true)
const newYear = ref(new Date().getFullYear())
const busy = ref('')
const error = ref('')
const message = ref('')

const tone = (status: string) =>
  status === 'Open' ? 'info' : status === 'Closed' ? 'warning' : 'neutral'

async function load() {
  years.value = await accountingApi.fiscalYears()
}

onMounted(async () => {
  try {
    await load()
  } finally {
    loading.value = false
  }
})

async function run(key: string, fn: () => Promise<unknown>) {
  busy.value = key
  error.value = ''
  message.value = ''
  try {
    await fn()
    await load()
  } catch (e) {
    error.value = e instanceof Error ? e.message : String(e)
  } finally {
    busy.value = ''
  }
}

const createYear = () => run('create', () => accountingApi.createFiscalYear(newYear.value))
const closePeriod = (id: string) => run(`cp-${id}`, () => accountingApi.closePeriod(id))
const reopenPeriod = (id: string) => run(`rp-${id}`, () => accountingApi.reopenPeriod(id))

function closeYear(y: FiscalYear) {
  if (!window.confirm(t('accounting.periods.confirmCloseYear', { year: y.year }))) return
  run(`cy-${y.id}`, async () => {
    const res = await accountingApi.closeFiscalYear(y.id)
    message.value = t('accounting.periods.closedResult', { amount: formatMoney(res.netIncome) })
  })
}
</script>

<template>
  <div class="space-y-4">
    <div class="flex flex-wrap items-end justify-between gap-3">
      <div class="flex items-end gap-2">
        <label class="text-sm">
          <span class="mb-1 block text-text-muted">{{ t('accounting.periods.yearLabel') }}</span>
          <input v-model.number="newYear" type="number" class="field-input w-28 tnum" />
        </label>
        <AppButton :disabled="busy === 'create'" @click="createYear">{{ t('accounting.periods.create') }}</AppButton>
      </div>
    </div>

    <p v-if="error" class="text-sm text-negative">{{ error }}</p>
    <p v-if="message" class="text-sm text-positive">{{ message }}</p>
    <p v-if="loading" class="text-sm text-text-muted">{{ t('common.loading') }}</p>
    <p v-else-if="years.length === 0" class="px-4 py-10 text-center text-sm text-text-muted">{{ t('accounting.periods.empty') }}</p>

    <AppCard v-for="y in years" :key="y.id" :padded="false">
      <div class="flex items-center justify-between border-b border-border px-4 py-2.5">
        <div class="flex items-center gap-2">
          <h3 class="text-sm font-semibold text-text">{{ y.year }}</h3>
          <span class="text-xs text-text-muted">{{ y.startDate }} → {{ y.endDate }}</span>
          <StatusBadge v-if="y.isClosed" tone="neutral" :label="t('accounting.periods.yearClosed')" />
        </div>
        <AppButton
          v-if="!y.isClosed"
          variant="secondary"
          :disabled="busy === `cy-${y.id}`"
          @click="closeYear(y)"
        >
          {{ busy === `cy-${y.id}` ? t('accounting.periods.closingYear') : t('accounting.periods.closeYear') }}
        </AppButton>
      </div>
      <table class="w-full text-sm">
        <thead>
          <tr class="border-b border-border text-xs uppercase tracking-wide text-text-muted">
            <th class="px-4 py-2 text-left font-semibold">{{ t('accounting.periods.period') }}</th>
            <th class="px-3 py-2 text-left font-semibold">{{ t('accounting.periods.range') }}</th>
            <th class="px-3 py-2 text-left font-semibold">{{ t('accounting.periods.status') }}</th>
            <th class="px-4 py-2 text-right font-semibold"></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="p in y.periods" :key="p.id" class="border-b border-border last:border-0">
            <td class="px-4 py-2 text-text tnum">{{ p.periodNo }}</td>
            <td class="px-3 py-2 text-text-muted tnum">{{ p.startDate }} → {{ p.endDate }}</td>
            <td class="px-3 py-2"><StatusBadge :tone="tone(p.status)" :label="p.status" /></td>
            <td class="px-4 py-2 text-right">
              <button
                v-if="p.status === 'Open'"
                class="rounded-md px-2 py-1 text-xs font-medium text-text-muted transition-colors hover:bg-surface-2 hover:text-text"
                :disabled="busy === `cp-${p.id}`"
                @click="closePeriod(p.id)"
              >
                {{ t('accounting.periods.closePeriod') }}
              </button>
              <button
                v-else-if="p.status === 'Closed'"
                class="rounded-md px-2 py-1 text-xs font-medium text-text-muted transition-colors hover:bg-surface-2 hover:text-text"
                :disabled="busy === `rp-${p.id}`"
                @click="reopenPeriod(p.id)"
              >
                {{ t('accounting.periods.reopenPeriod') }}
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </AppCard>
  </div>
</template>
