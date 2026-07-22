<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { Plus, Trash2 } from 'lucide-vue-next'
import { accountingApi } from '@/lib/accounting'
import { accountOptionLabel } from '@/lib/coa'
import { formatMoney } from '@/lib/format'
import type { Locale } from '@/i18n'
import type { AccountRef, JournalEntry, JournalLineInput, JournalRegisterItem } from '@/types/accounting'
import type { Column, SelectOption } from '@/components/ui/types'
import { useAuthStore } from '@/stores/auth'
import AppButton from '@/components/ui/AppButton.vue'
import AppInput from '@/components/ui/AppInput.vue'
import AppSelect from '@/components/ui/AppSelect.vue'
import AppModal from '@/components/ui/AppModal.vue'
import DataTable from '@/components/ui/DataTable.vue'
import FormField from '@/components/ui/FormField.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'

const { t, locale } = useI18n()
const loc = computed(() => locale.value as Locale)
const auth = useAuthStore()

const rows = ref<JournalRegisterItem[]>([])
const accounts = ref<AccountRef[]>([])
const loading = ref(true)
const canPost = computed(() => auth.has('Accounting.Post'))

const today = () => {
  const d = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

const accountOptions = computed<SelectOption[]>(() =>
  accounts.value
    .filter((a) => a.isActive && a.allowPosting)
    .map((a) => ({ value: a.id, label: accountOptionLabel(a, loc.value) })),
)

const columns = computed<Column[]>(() => [
  { key: 'entryNo', label: t('accounting.journals.columns.entryNo') },
  { key: 'date', label: t('accounting.journals.columns.date') },
  { key: 'source', label: t('accounting.journals.columns.source'), hideOnMobile: true },
  { key: 'description', label: t('accounting.journals.columns.description') },
  { key: 'amount', label: t('accounting.journals.columns.amount'), align: 'right' },
  { key: 'status', label: t('accounting.journals.columns.status') },
])

async function load() {
  loading.value = true
  try {
    ;[rows.value, accounts.value] = await Promise.all([
      accountingApi.journalEntries(),
      accounts.value.length ? Promise.resolve(accounts.value) : accountingApi.accounts(),
    ])
  } finally {
    loading.value = false
  }
}
onMounted(load)

// --- New journal ---
const formOpen = ref(false)
const saving = ref(false)
const error = ref('')
const banner = ref('')
const form = reactive<{ date: string; description: string; lines: JournalLineInput[] }>({
  date: today(),
  description: '',
  lines: [],
})

function blankLine(): JournalLineInput {
  return { accountId: '', debit: '', credit: '', description: '' }
}

function openNew() {
  form.date = today()
  form.description = ''
  form.lines = [blankLine(), blankLine()]
  error.value = ''
  formOpen.value = true
}

const totals = computed(() => {
  const debit = form.lines.reduce((s, l) => s + (parseFloat(l.debit) || 0), 0)
  const credit = form.lines.reduce((s, l) => s + (parseFloat(l.credit) || 0), 0)
  return { debit, credit, balanced: Math.round((debit - credit) * 10000) === 0 && debit > 0 }
})

async function save() {
  error.value = ''
  const lines = form.lines
    .filter((l) => l.accountId && ((parseFloat(l.debit) || 0) > 0 || (parseFloat(l.credit) || 0) > 0))
    .map((l) => ({
      accountId: l.accountId,
      debit: parseFloat(l.debit) || 0,
      credit: parseFloat(l.credit) || 0,
      description: l.description || null,
    }))
  if (lines.length < 2) {
    error.value = t('accounting.journals.needLines')
    return
  }
  if (!totals.value.balanced) {
    error.value = t('accounting.journals.outOfBalance')
    return
  }
  saving.value = true
  try {
    const res = await accountingApi.postJournal({ date: form.date, description: form.description, lines })
    formOpen.value = false
    banner.value = res.status === 'Pending' ? t('accounting.journals.pendingResult') : t('accounting.journals.postedResult')
    await load()
  } catch (e) {
    error.value = e instanceof Error ? e.message : t('accounting.journals.failed')
  } finally {
    saving.value = false
  }
}

// --- Detail + reverse ---
const detail = ref<JournalEntry | null>(null)
const detailOpen = ref(false)
const reversing = ref(false)

async function openDetail(row: Record<string, unknown>) {
  detail.value = await accountingApi.journalEntry((row as unknown as JournalRegisterItem).id)
  detailOpen.value = true
}

async function reverse() {
  if (!detail.value) return
  error.value = ''
  reversing.value = true
  try {
    await accountingApi.reverseJournal(detail.value.id, { date: null, reason: null })
    detailOpen.value = false
    await load()
  } catch (e) {
    error.value = e instanceof Error ? e.message : t('accounting.journals.reverseFailed')
  } finally {
    reversing.value = false
  }
}
</script>

<template>
  <div class="space-y-4">
    <div class="flex items-center justify-between gap-3">
      <p class="text-sm text-text-muted">{{ t('accounting.journals.subtitle') }}</p>
      <AppButton v-if="canPost" @click="openNew"><Plus :size="16" /> {{ t('accounting.journals.new') }}</AppButton>
    </div>

    <p v-if="banner" class="rounded-control bg-positive/10 px-3 py-2 text-sm text-positive">{{ banner }}</p>

    <DataTable
      searchable
      clickable
      :columns="columns"
      :rows="rows"
      :loading="loading"
      :empty-text="t('accounting.journals.empty')"
      @row-click="openDetail"
    >
      <template #cell-entryNo="{ row }">
        <span class="tnum text-text-muted">{{ (row as unknown as JournalRegisterItem).entryNo ?? '—' }}</span>
      </template>
      <template #cell-date="{ row }">
        <span class="tnum">{{ (row as unknown as JournalRegisterItem).date }}</span>
      </template>
      <template #cell-source="{ row }">{{ t(`accounting.sources.${(row as unknown as JournalRegisterItem).source}`) }}</template>
      <template #cell-amount="{ row }">
        <span class="tnum">{{ formatMoney((row as unknown as JournalRegisterItem).amount) }}</span>
      </template>
      <template #cell-status="{ row }">
        <StatusBadge
          :status="(row as unknown as JournalRegisterItem).status"
          :label="t(`accounting.journalStatus.${(row as unknown as JournalRegisterItem).status}`)"
        />
      </template>
    </DataTable>

    <!-- New journal -->
    <AppModal v-model="formOpen" :title="t('accounting.journals.new')" size="lg">
      <div class="space-y-4">
        <div class="grid grid-cols-2 gap-4">
          <FormField :label="t('accounting.journals.date')" required>
            <AppInput v-model="form.date" type="date" />
          </FormField>
          <FormField :label="t('accounting.journals.description')" required>
            <AppInput v-model="form.description" :placeholder="t('accounting.journals.descriptionPlaceholder')" />
          </FormField>
        </div>

        <div>
          <p class="mb-1.5 text-sm font-medium text-text">{{ t('accounting.journals.lines') }}</p>
          <div class="space-y-2">
            <div v-for="(line, i) in form.lines" :key="i" class="grid grid-cols-12 items-center gap-2">
              <div class="col-span-5">
                <AppSelect v-model="line.accountId" :options="accountOptions" :placeholder="t('accounting.journals.selectAccount')" />
              </div>
              <div class="col-span-3">
                <AppInput v-model="line.debit" type="number" :placeholder="t('accounting.journals.debit')" />
              </div>
              <div class="col-span-3">
                <AppInput v-model="line.credit" type="number" :placeholder="t('accounting.journals.credit')" />
              </div>
              <div class="col-span-1 flex justify-end">
                <button
                  type="button"
                  class="grid h-8 w-8 place-items-center rounded-control text-text-muted hover:bg-surface-2 hover:text-negative"
                  :disabled="form.lines.length <= 2"
                  @click="form.lines.splice(i, 1)"
                >
                  <Trash2 :size="15" />
                </button>
              </div>
            </div>
          </div>
          <button type="button" class="mt-2 inline-flex items-center gap-1 text-sm font-medium text-accent hover:underline" @click="form.lines.push(blankLine())">
            <Plus :size="14" /> {{ t('accounting.journals.addLine') }}
          </button>
        </div>

        <div class="flex items-center justify-between border-t border-border pt-3 text-sm">
          <span class="text-text-muted">{{ t('accounting.journals.totals') }}</span>
          <div class="flex items-center gap-4 tnum">
            <span>{{ formatMoney(totals.debit) }}</span>
            <span>{{ formatMoney(totals.credit) }}</span>
            <span :class="totals.balanced ? 'text-positive' : 'text-negative'">
              {{ totals.balanced ? t('accounting.journals.balanced') : t('accounting.journals.outOfBalance') }}
            </span>
          </div>
        </div>

        <p v-if="error" class="text-sm text-negative">{{ error }}</p>
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="formOpen = false">{{ t('masterData.cancel') }}</AppButton>
        <AppButton :disabled="saving || !form.description || !totals.balanced" @click="save">
          {{ saving ? t('accounting.journals.saving') : t('accounting.journals.save') }}
        </AppButton>
      </template>
    </AppModal>

    <!-- Detail + reverse -->
    <AppModal v-model="detailOpen" :title="detail?.entryNo ?? t('accounting.journals.pending')" size="lg">
      <div v-if="detail" class="space-y-3">
        <div class="flex flex-wrap items-center gap-x-6 gap-y-1 text-sm">
          <span class="tnum text-text-muted">{{ detail.date }}</span>
          <span>{{ t(`accounting.sources.${detail.source}`) }}</span>
          <StatusBadge :status="detail.status" :label="t(`accounting.journalStatus.${detail.status}`)" />
        </div>
        <p class="text-sm text-text">{{ detail.description }}</p>
        <table class="w-full text-sm">
          <thead>
            <tr class="border-b border-border text-xs uppercase tracking-wide text-text-muted">
              <th class="px-2 py-1.5 text-left font-semibold">{{ t('accounting.journals.account') }}</th>
              <th class="px-2 py-1.5 text-right font-semibold">{{ t('accounting.journals.debit') }}</th>
              <th class="px-2 py-1.5 text-right font-semibold">{{ t('accounting.journals.credit') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(l, i) in detail.lines" :key="i" class="border-b border-border">
              <td class="px-2 py-1.5">
                <template v-if="accounts.find((a) => a.id === l.accountId)">
                  {{ accountOptionLabel(accounts.find((a) => a.id === l.accountId)!, loc) }}
                </template>
                <span v-if="l.description" class="text-text-muted"> — {{ l.description }}</span>
              </td>
              <td class="px-2 py-1.5 text-right tnum">{{ l.debit ? formatMoney(l.debit) : '' }}</td>
              <td class="px-2 py-1.5 text-right tnum">{{ l.credit ? formatMoney(l.credit) : '' }}</td>
            </tr>
          </tbody>
          <tfoot>
            <tr class="border-t-2 border-border font-semibold">
              <td class="px-2 py-1.5 text-right">{{ t('accounting.journals.totals') }}</td>
              <td class="px-2 py-1.5 text-right tnum">{{ formatMoney(detail.totalDebit) }}</td>
              <td class="px-2 py-1.5 text-right tnum">{{ formatMoney(detail.totalCredit) }}</td>
            </tr>
          </tfoot>
        </table>
        <p v-if="detail.status === 'PendingApproval'" class="text-sm text-warning">{{ t('accounting.journals.pendingHint') }}</p>
        <p v-if="error" class="text-sm text-negative">{{ error }}</p>
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="detailOpen = false">{{ t('masterData.close') }}</AppButton>
        <AppButton
          v-if="canPost && detail?.status === 'Posted'"
          variant="secondary"
          :disabled="reversing"
          @click="reverse"
        >
          {{ reversing ? t('accounting.journals.reversing') : t('accounting.journals.reverse') }}
        </AppButton>
      </template>
    </AppModal>
  </div>
</template>
