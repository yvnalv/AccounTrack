<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { RouterLink, RouterView } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useCompanyStore } from '@/stores/company'
import { useAuthStore } from '@/stores/auth'

const { t } = useI18n()
const company = useCompanyStore()
const auth = useAuthStore()

onMounted(() => company.ensure())

const tabs = computed(() => {
  const all = [
    { to: { name: 'accountingTrialBalance' }, label: t('accounting.tabs.trialBalance') },
    { to: { name: 'accountingProfitLoss' }, label: t('accounting.tabs.profitLoss') },
    { to: { name: 'accountingBalanceSheet' }, label: t('accounting.tabs.balanceSheet') },
    { to: { name: 'accountingCashFlow' }, label: t('accounting.tabs.cashFlow') },
    { to: { name: 'accountingGeneralLedger' }, label: t('accounting.tabs.generalLedger') },
    { to: { name: 'accountingJournals' }, label: t('accounting.tabs.journals') },
    // Guided Cash & Bank flows create journals — only for users who may post (ADR-0040).
    ...(auth.has('Accounting.Post') ? [{ to: { name: 'accountingCashBank' }, label: t('accounting.tabs.cashBank') }] : []),
    // VAT report only matters for a VAT-registered (PKP) company.
    ...(company.vatRegistered ? [{ to: { name: 'accountingVat' }, label: t('accounting.tabs.vat') }] : []),
    { to: { name: 'accountingAccounts' }, label: t('accounting.tabs.accounts') },
    { to: { name: 'accountingPeriods' }, label: t('accounting.tabs.periods') },
  ]
  return all
})
</script>

<template>
  <div class="space-y-5">
    <nav class="flex flex-wrap gap-1 border-b border-border">
      <RouterLink
        v-for="tab in tabs"
        :key="tab.label"
        :to="tab.to"
        class="-mb-px border-b-2 border-transparent px-3 py-2 text-sm font-medium text-text-muted transition-colors hover:text-text"
        active-class="!border-accent !text-text"
      >
        {{ tab.label }}
      </RouterLink>
    </nav>

    <RouterView />
  </div>
</template>
