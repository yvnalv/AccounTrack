import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { companyApi } from '@/lib/company'
import { useAuthStore } from './auth'
import type { Company } from '@/types/company'

/** The active company's configuration (VAT/PKP status, currency), shared app-wide. */
export const useCompanyStore = defineStore('company', () => {
  const company = ref<Company | null>(null)
  const loaded = ref(false)

  async function refresh() {
    const auth = useAuthStore()
    const list = await companyApi.list()
    const preferred = auth.user?.companyIds?.[0]
    company.value = list.find((c) => c.id === preferred) ?? list[0] ?? null
    loaded.value = true
  }

  /** Load once (idempotent) — call from views that need the company config. */
  async function ensure() {
    if (!loaded.value) await refresh()
  }

  function set(c: Company) {
    company.value = c
    loaded.value = true
  }

  /** Whether the active company is registered for VAT (Indonesian PKP). */
  const vatRegistered = computed(() => company.value?.isVatRegistered ?? false)
  /** Default tax rate for new transaction lines (0 when not VAT-registered). */
  const defaultTaxRate = computed(() => (vatRegistered.value ? 0.11 : 0))
  const currency = computed(() => company.value?.functionalCurrency ?? 'IDR')

  return { company, loaded, ensure, refresh, set, vatRegistered, defaultTaxRate, currency }
})
