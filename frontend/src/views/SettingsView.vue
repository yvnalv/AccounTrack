<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { Sun, Moon, Check } from 'lucide-vue-next'
import { companyApi } from '@/lib/company'
import { useAuthStore } from '@/stores/auth'
import { useThemeStore } from '@/stores/theme'
import { useCompanyStore } from '@/stores/company'
import { persistLocale } from '@/i18n'
import type { Company } from '@/types/company'
import AppCard from '@/components/ui/AppCard.vue'
import AppButton from '@/components/ui/AppButton.vue'
import AppInput from '@/components/ui/AppInput.vue'
import AppSelect from '@/components/ui/AppSelect.vue'
import FormField from '@/components/ui/FormField.vue'
import RolesManager from '@/components/settings/RolesManager.vue'

const { t, locale } = useI18n()
const auth = useAuthStore()
const theme = useThemeStore()
const companyStore = useCompanyStore()

const canEditCompany = computed(() => auth.has('Admin.Companies'))
const canManageRoles = computed(() => auth.has('Admin.Roles'))

// --- Company ---
const companies = ref<Company[]>([])
const selectedId = ref('')
const loading = ref(true)
const saving = ref(false)
const message = ref<{ kind: 'ok' | 'err'; text: string } | null>(null)
const form = reactive({
  name: '',
  legalName: '',
  taxId: '',
  timeZone: '',
  isVatRegistered: false,
  allowNegativeStock: false,
})

const selected = computed(() => companies.value.find((c) => c.id === selectedId.value) ?? null)
const companyOptions = computed(() => companies.value.map((c) => ({ value: c.id, label: `${c.code} — ${c.name}` })))

function monthName(month: number): string {
  return new Date(2000, month - 1, 1).toLocaleString(locale.value, { month: 'long' })
}

function syncForm(c: Company | null) {
  form.name = c?.name ?? ''
  form.legalName = c?.legalName ?? ''
  form.taxId = c?.taxId ?? ''
  form.timeZone = c?.timeZone ?? ''
  form.isVatRegistered = c?.isVatRegistered ?? false
  form.allowNegativeStock = c?.allowNegativeStock ?? false
}

watch(selected, syncForm)

async function loadCompanies() {
  loading.value = true
  try {
    companies.value = await companyApi.list()
    const preferred = auth.user?.companyIds?.[0]
    selectedId.value =
      (preferred && companies.value.some((c) => c.id === preferred) ? preferred : companies.value[0]?.id) ?? ''
    syncForm(selected.value)
  } finally {
    loading.value = false
  }
}
onMounted(loadCompanies)

async function saveCompany() {
  if (!selected.value || !canEditCompany.value) return
  message.value = null
  saving.value = true
  try {
    await companyApi.update(selected.value.id, {
      name: form.name,
      legalName: form.legalName || null,
      taxId: form.taxId || null,
      timeZone: form.timeZone,
      isVatRegistered: form.isVatRegistered,
    })
    if (form.allowNegativeStock !== selected.value.allowNegativeStock) {
      await companyApi.setSetting(selected.value.id, 'Inventory.AllowNegativeStock', String(form.allowNegativeStock))
    }
    await loadCompanies()
    await companyStore.refresh() // keep the app-wide VAT/PKP flag in sync for the create forms
    message.value = { kind: 'ok', text: t('settings.company.saved') }
  } catch {
    message.value = { kind: 'err', text: t('settings.company.failed') }
  } finally {
    saving.value = false
  }
}

// --- Preferences ---
const themes = [
  { value: 'light' as const, label: 'theme.light', icon: Sun },
  { value: 'dark' as const, label: 'theme.dark', icon: Moon },
]
const localeOptions = computed(() => [
  { value: 'en', label: 'English' },
  { value: 'id', label: 'Bahasa Indonesia' },
])
function setLocale(next: string | undefined) {
  if (next !== 'en' && next !== 'id') return
  locale.value = next
  persistLocale(next)
}
</script>

<template>
  <div class="grid max-w-4xl gap-6">
    <!-- Company -->
    <AppCard :title="t('settings.company.title')">
      <div v-if="loading" class="text-sm text-text-muted">{{ t('common.loading') }}</div>
      <div v-else-if="!selected" class="text-sm text-text-muted">{{ t('settings.company.none') }}</div>
      <div v-else class="space-y-4">
        <FormField v-if="companies.length > 1" :label="t('settings.company.select')">
          <AppSelect v-model="selectedId" :options="companyOptions" />
        </FormField>

        <div class="grid grid-cols-1 gap-4 sm:grid-cols-3">
          <div>
            <p class="text-xs text-text-muted">{{ t('settings.company.code') }}</p>
            <p class="font-medium text-text">{{ selected.code }}</p>
          </div>
          <div>
            <p class="text-xs text-text-muted">{{ t('settings.company.currency') }}</p>
            <p class="font-medium text-text">{{ selected.functionalCurrency }}</p>
          </div>
          <div>
            <p class="text-xs text-text-muted">{{ t('settings.company.fiscalStart') }}</p>
            <p class="font-medium text-text">{{ monthName(selected.fiscalYearStartMonth) }}</p>
          </div>
        </div>

        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <FormField :label="t('settings.company.name')" required>
            <AppInput v-model="form.name" :disabled="!canEditCompany" />
          </FormField>
          <FormField :label="t('settings.company.legalName')">
            <AppInput v-model="form.legalName" :disabled="!canEditCompany" />
          </FormField>
          <FormField :label="t('settings.company.taxId')">
            <AppInput v-model="form.taxId" :disabled="!canEditCompany" />
          </FormField>
          <FormField :label="t('settings.company.timeZone')">
            <AppInput v-model="form.timeZone" :disabled="!canEditCompany" />
          </FormField>
        </div>

        <!-- VAT / PKP -->
        <label class="flex items-start gap-3 rounded-card border border-border bg-surface-2 px-4 py-3">
          <input
            v-model="form.isVatRegistered"
            type="checkbox"
            class="mt-0.5 h-4 w-4 accent-accent"
            :disabled="!canEditCompany"
          />
          <span class="text-sm">
            <span class="font-medium text-text">{{ t('settings.company.vatRegistered') }}</span>
            <span class="mt-0.5 block text-xs text-text-muted">{{ t('settings.company.vatHint') }}</span>
          </span>
        </label>

        <!-- Inventory: negative-stock policy -->
        <label class="flex items-start gap-3 rounded-card border border-border bg-surface-2 px-4 py-3">
          <input
            v-model="form.allowNegativeStock"
            type="checkbox"
            class="mt-0.5 h-4 w-4 accent-accent"
            :disabled="!canEditCompany"
          />
          <span class="text-sm">
            <span class="font-medium text-text">{{ t('settings.inventory.allowNegative') }}</span>
            <span class="mt-0.5 block text-xs text-text-muted">{{ t('settings.inventory.allowNegativeHint') }}</span>
          </span>
        </label>

        <p v-if="!canEditCompany" class="text-xs text-text-muted">{{ t('settings.company.readOnly') }}</p>
        <p
          v-if="message"
          class="text-sm"
          :class="message.kind === 'ok' ? 'text-positive' : 'text-negative'"
        >
          {{ message.text }}
        </p>

        <div v-if="canEditCompany" class="flex justify-end">
          <AppButton :disabled="saving || !form.name" @click="saveCompany">
            {{ saving ? t('settings.company.saving') : t('settings.company.save') }}
          </AppButton>
        </div>
      </div>
    </AppCard>

    <!-- Roles & access -->
    <AppCard v-if="canManageRoles" :title="t('settings.roles.title')">
      <RolesManager />
    </AppCard>

    <!-- Profile -->
    <AppCard :title="t('settings.profile.title')">
      <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <div>
          <p class="text-xs text-text-muted">{{ t('settings.profile.name') }}</p>
          <p class="font-medium text-text">{{ auth.user?.fullName || '—' }}</p>
        </div>
        <div>
          <p class="text-xs text-text-muted">{{ t('settings.profile.email') }}</p>
          <p class="font-medium text-text">{{ auth.user?.email || '—' }}</p>
        </div>
        <div class="sm:col-span-2">
          <p class="text-xs text-text-muted">{{ t('settings.profile.roles') }}</p>
          <p class="font-medium text-text">{{ auth.user?.roles?.join(', ') || '—' }}</p>
        </div>
      </div>
    </AppCard>

    <!-- Preferences -->
    <AppCard :title="t('settings.prefs.title')">
      <div class="space-y-5">
        <div>
          <p class="mb-2 text-xs text-text-muted">{{ t('settings.prefs.theme') }}</p>
          <div class="flex gap-2">
            <button
              v-for="opt in themes"
              :key="opt.value"
              type="button"
              class="flex h-9 items-center gap-2 rounded-full border px-4 text-xs font-semibold transition-colors"
              :class="theme.theme === opt.value
                ? 'border-accent bg-accent-soft text-accent'
                : 'border-border bg-surface text-text-muted hover:text-text'"
              @click="theme.apply(opt.value)"
            >
              <component :is="opt.icon" :size="16" />
              {{ t(opt.label) }}
              <Check v-if="theme.theme === opt.value" :size="14" />
            </button>
          </div>
        </div>

        <FormField :label="t('settings.prefs.language')">
          <AppSelect
            :options="localeOptions"
            :model-value="locale"
            @update:model-value="setLocale"
          />
        </FormField>
      </div>
    </AppCard>
  </div>
</template>
