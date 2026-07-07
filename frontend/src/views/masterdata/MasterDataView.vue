<script setup lang="ts">
import { RouterLink, RouterView } from 'vue-router'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

// Setup page — supporting master data (Products/Customers/Suppliers/Warehouses are their own pages).
const tabs = [
  { to: { name: 'masterDataUnits' }, label: t('masterData.tabs.unitsOfMeasure') },
  { to: { name: 'masterDataCategories' }, label: t('masterData.tabs.categories') },
  { to: { name: 'masterDataTaxCodes' }, label: t('masterData.tabs.taxCodes') },
  { to: { name: 'masterDataPriceLists' }, label: t('masterData.tabs.priceLists') },
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
