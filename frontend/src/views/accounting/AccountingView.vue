<script setup lang="ts">
import { RouterLink, RouterView } from 'vue-router'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

const tabs = [
  { to: { name: 'accountingTrialBalance' }, label: t('accounting.tabs.trialBalance') },
  { to: { name: 'accountingProfitLoss' }, label: t('accounting.tabs.profitLoss') },
  { to: { name: 'accountingBalanceSheet' }, label: t('accounting.tabs.balanceSheet') },
  { to: { name: 'accountingCashFlow' }, label: t('accounting.tabs.cashFlow') },
  { to: { name: 'accountingGeneralLedger' }, label: t('accounting.tabs.generalLedger') },
  { to: { name: 'accountingVat' }, label: t('accounting.tabs.vat') },
  { to: { name: 'accountingPeriods' }, label: t('accounting.tabs.periods') },
]
</script>

<template>
  <div class="space-y-5">
    <nav class="flex gap-1 border-b border-border">
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
