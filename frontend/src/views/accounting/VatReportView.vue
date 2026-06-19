<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { reportsApi } from '@/lib/reports'
import { formatMoney } from '@/lib/format'
import type { VatReport } from '@/types/reports'
import AppButton from '@/components/ui/AppButton.vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppInput from '@/components/ui/AppInput.vue'
import FormField from '@/components/ui/FormField.vue'

const { t } = useI18n()

const now = new Date()
const monthStart = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-01`
const today = now.toISOString().slice(0, 10)

const fromDate = ref(monthStart)
const toDate = ref(today)
const report = ref<VatReport | null>(null)
const loading = ref(true)

async function load() {
  loading.value = true
  try {
    report.value = await reportsApi.vat({
      fromDate: fromDate.value || undefined,
      toDate: toDate.value || undefined,
    })
  } finally {
    loading.value = false
  }
}
onMounted(load)
</script>

<template>
  <div class="space-y-4">
    <div class="flex flex-wrap items-end gap-3">
      <FormField :label="t('accounting.filters.from')"><AppInput v-model="fromDate" type="date" /></FormField>
      <FormField :label="t('accounting.filters.to')"><AppInput v-model="toDate" type="date" /></FormField>
      <AppButton variant="secondary" :disabled="loading" @click="load">{{ t('accounting.filters.apply') }}</AppButton>
    </div>

    <p v-if="loading" class="text-sm text-text-muted">{{ t('common.loading') }}</p>

    <AppCard v-else-if="report" :padded="false">
      <table class="w-full text-sm">
        <tbody>
          <tr class="border-b border-border">
            <td class="px-4 py-2.5">
              <span class="text-text">{{ t('accounting.vat.outputTax') }}</span>
              <span class="ml-2 text-xs text-text-muted">{{ report.outputAccountCode }} · {{ report.outputAccountName }}</span>
            </td>
            <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(report.outputTax) }}</td>
          </tr>
          <tr class="border-b border-border">
            <td class="px-4 py-2.5">
              <span class="text-text">{{ t('accounting.vat.inputTax') }}</span>
              <span class="ml-2 text-xs text-text-muted">{{ report.inputAccountCode }} · {{ report.inputAccountName }}</span>
            </td>
            <td class="px-4 py-2.5 text-right text-text tnum">({{ formatMoney(report.inputTax) }})</td>
          </tr>
        </tbody>
        <tfoot>
          <tr class="border-t-2 border-border text-base font-semibold">
            <td class="px-4 py-3 text-text">
              {{ report.netVatPayable >= 0 ? t('accounting.vat.payable') : t('accounting.vat.creditCarried') }}
            </td>
            <td class="px-4 py-3 text-right tnum" :class="report.netVatPayable >= 0 ? 'text-text' : 'text-positive'">
              {{ formatMoney(report.netVatPayable) }}
            </td>
          </tr>
        </tfoot>
      </table>
    </AppCard>

    <p class="text-xs text-text-muted">{{ t('accounting.vat.note') }}</p>
  </div>
</template>
