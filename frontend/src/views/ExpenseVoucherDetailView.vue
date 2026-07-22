<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { ArrowLeft, Pencil, Undo2 } from 'lucide-vue-next'
import { apiErrorMessage } from '@/lib/api'
import { expensesApi } from '@/lib/expenses'
import { accountingApi } from '@/lib/accounting'
import { localizedAccountName } from '@/lib/coa'
import { localizedCategoryName } from '@/lib/expenseCategories'
import { masterData, nameMap } from '@/lib/masterData'
import { useAuthStore } from '@/stores/auth'
import { formatMoney, formatPercent } from '@/lib/format'
import type { Locale } from '@/i18n'
import type { ExpenseCategory, ExpenseVoucher } from '@/types/expenses'
import type { AccountRef } from '@/types/accounting'
import AppButton from '@/components/ui/AppButton.vue'
import AppCard from '@/components/ui/AppCard.vue'
import AppInput from '@/components/ui/AppInput.vue'
import AppModal from '@/components/ui/AppModal.vue'
import FormField from '@/components/ui/FormField.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import DocumentTimeline from '@/components/DocumentTimeline.vue'

const { t, locale } = useI18n()
const loc = computed(() => locale.value as Locale)
const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const voucher = ref<ExpenseVoucher | null>(null)
const categories = ref(new Map<string, string>())
const suppliers = ref(new Map<string, string>())
const accounts = ref(new Map<string, string>())
const loading = ref(true)
const busy = ref<'' | 'submit' | 'cancel' | 'reverse'>('')
const error = ref('')

const id = computed(() => String(route.params.id))
const currency = computed(() => voucher.value?.currency ?? 'IDR')

const today = () => new Date().toISOString().slice(0, 10)

// Reverse modal
const reverseModal = ref(false)
const reverseDate = ref(today())
const reverseReason = ref('')

async function load() {
  loading.value = true
  try {
    const [v, cats, sups, accs] = await Promise.all([
      expensesApi.getVoucher(id.value),
      expensesApi.categories(),
      masterData.suppliers(),
      accountingApi.accounts(),
    ])
    voucher.value = v
    categories.value = new Map(cats.map((c: ExpenseCategory) => [c.id, `${c.code} — ${localizedCategoryName(c, loc.value)}`]))
    suppliers.value = nameMap(sups)
    accounts.value = new Map(accs.map((a: AccountRef) => [a.id, `${a.code} — ${localizedAccountName(a, loc.value)}`]))
  } finally {
    loading.value = false
  }
}
onMounted(load)

const canEdit = computed(() => voucher.value?.status === 'Draft' && auth.has('Expenses.Edit'))
const canCancel = computed(() => voucher.value?.status === 'Draft' && auth.has('Expenses.Cancel'))
const canSubmit = computed(() => voucher.value?.status === 'Draft' && auth.has('Expenses.Post'))
const canReverse = computed(() => voucher.value?.status === 'Posted' && auth.has('Expenses.Post'))

async function run(kind: 'submit' | 'cancel' | 'reverse', fn: () => Promise<unknown>) {
  if (!voucher.value) return
  busy.value = kind
  error.value = ''
  try {
    await fn()
    await load()
  } catch (e) {
    error.value = apiErrorMessage(e, t('expenses.detail.actionFailed'))
  } finally {
    busy.value = ''
  }
}

const submit = () => run('submit', () => expensesApi.submitVoucher(voucher.value!.id))

function cancel() {
  if (!voucher.value || !window.confirm(t('expenses.detail.confirmCancel'))) return
  run('cancel', () => expensesApi.cancelVoucher(voucher.value!.id))
}

function openReverse() {
  reverseDate.value = today()
  reverseReason.value = ''
  error.value = ''
  reverseModal.value = true
}
async function confirmReverse() {
  reverseModal.value = false
  await run('reverse', () =>
    expensesApi.reverseVoucher(voucher.value!.id, { date: reverseDate.value, reason: reverseReason.value || null }),
  )
}
</script>

<template>
  <div class="space-y-5">
    <button
      class="inline-flex items-center gap-1.5 text-sm text-text-muted hover:text-text"
      @click="router.push({ name: 'expenses' })"
    >
      <ArrowLeft :size="16" /> {{ t('expenses.backToList') }}
    </button>

    <p v-if="loading" class="text-sm text-text-muted">{{ t('common.loading') }}</p>

    <template v-else-if="voucher">
      <!-- Header + actions -->
      <div class="flex flex-wrap items-start justify-between gap-4">
        <div>
          <div class="flex items-center gap-3">
            <h2 class="text-xl font-semibold text-text">{{ voucher.number }}</h2>
            <StatusBadge :status="voucher.status" :label="t(`expenses.statusLabel.${voucher.status}`)" />
            <StatusBadge
              v-if="voucher.supplierId && voucher.status === 'Posted'"
              tone="warning"
              :label="t('expenses.unpaid')"
            />
          </div>
          <p class="mt-1 text-sm text-text-muted">
            {{ voucher.payeeName || (voucher.supplierId ? suppliers.get(voucher.supplierId) : '—') }} · {{ voucher.expenseDate }}
          </p>
        </div>
        <div class="flex flex-wrap items-center gap-2">
          <AppButton
            v-if="canEdit"
            variant="secondary"
            :disabled="busy !== ''"
            @click="router.push({ name: 'expenseCreate', query: { edit: voucher.id } })"
          >
            <Pencil :size="16" /> {{ t('expenses.detail.edit') }}
          </AppButton>
          <AppButton v-if="canSubmit" :disabled="busy !== ''" @click="submit">
            {{ busy === 'submit' ? t('expenses.detail.submitting') : t('expenses.detail.submit') }}
          </AppButton>
          <AppButton v-if="canReverse" variant="danger" :disabled="busy !== ''" @click="openReverse">
            <Undo2 :size="16" /> {{ busy === 'reverse' ? t('expenses.detail.reversing') : t('expenses.detail.reverse') }}
          </AppButton>
          <AppButton v-if="canCancel" variant="danger" :disabled="busy !== ''" @click="cancel">
            {{ busy === 'cancel' ? t('expenses.detail.cancelling') : t('expenses.detail.cancel') }}
          </AppButton>
        </div>
      </div>

      <p v-if="error" class="text-sm text-negative">{{ error }}</p>

      <!-- Lines -->
      <AppCard :title="t('expenses.detail.lines')" :padded="false">
        <div class="overflow-x-auto">
          <table class="w-full text-sm">
            <thead>
              <tr class="border-b border-border text-xs uppercase tracking-wide text-text-muted">
                <th class="px-4 py-2.5 text-left font-semibold">{{ t('expenses.detail.category') }}</th>
                <th class="px-3 py-2.5 text-left font-semibold">{{ t('expenses.detail.description') }}</th>
                <th class="px-3 py-2.5 text-right font-semibold">{{ t('expenses.detail.amount') }}</th>
                <th class="px-3 py-2.5 text-right font-semibold">{{ t('expenses.detail.taxPct') }}</th>
                <th class="px-4 py-2.5 text-right font-semibold">{{ t('expenses.detail.lineTotal') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(line, i) in voucher.lines" :key="i" class="border-b border-border last:border-0">
                <td class="px-4 py-3 text-text">{{ categories.get(line.expenseCategoryId) ?? '—' }}</td>
                <td class="px-3 py-3 text-text-muted">{{ line.description || '—' }}</td>
                <td class="px-3 py-3 text-right text-text tnum">{{ formatMoney(line.amount, currency) }}</td>
                <td class="px-3 py-3 text-right text-text-muted tnum">{{ formatPercent(line.taxRate * 100) }}</td>
                <td class="px-4 py-3 text-right text-text tnum">{{ formatMoney(line.lineTotal, currency) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <div class="flex justify-end border-t border-border p-4">
          <dl class="w-full max-w-xs space-y-1.5 text-sm">
            <div class="flex justify-between">
              <dt class="text-text-muted">{{ t('expenses.detail.subtotal') }}</dt>
              <dd class="tnum text-text">{{ formatMoney(voucher.subTotal, currency) }}</dd>
            </div>
            <div class="flex justify-between">
              <dt class="text-text-muted">{{ t('expenses.detail.taxTotal') }}</dt>
              <dd class="tnum text-text">{{ formatMoney(voucher.taxTotal, currency) }}</dd>
            </div>
            <div class="flex justify-between border-t border-border pt-1.5 font-semibold">
              <dt class="text-text">{{ t('expenses.detail.grandTotal') }}</dt>
              <dd class="tnum text-text">{{ formatMoney(voucher.grandTotal, currency) }}</dd>
            </div>
          </dl>
        </div>
      </AppCard>

      <!-- Meta -->
      <AppCard>
        <dl class="grid grid-cols-1 gap-x-8 gap-y-3 text-sm sm:grid-cols-2">
          <div class="flex justify-between border-b border-border pb-2">
            <dt class="text-text-muted">{{ voucher.supplierId ? t('expenses.detail.onAccountTo') : t('expenses.detail.paidFrom') }}</dt>
            <dd class="text-text">
              {{ voucher.supplierId ? (suppliers.get(voucher.supplierId) ?? '—') : (voucher.cashAccountId ? (accounts.get(voucher.cashAccountId) ?? '—') : '—') }}
            </dd>
          </div>
          <div v-if="voucher.dueDate" class="flex justify-between border-b border-border pb-2">
            <dt class="text-text-muted">{{ t('expenses.fields.dueDate') }}</dt>
            <dd class="text-text">{{ voucher.dueDate }}</dd>
          </div>
          <div v-if="voucher.reference" class="flex justify-between border-b border-border pb-2">
            <dt class="text-text-muted">{{ t('expenses.detail.reference') }}</dt>
            <dd class="text-text">{{ voucher.reference }}</dd>
          </div>
          <div v-if="voucher.journalEntryId" class="flex justify-between border-b border-border pb-2">
            <dt class="text-text-muted">{{ t('expenses.detail.journal') }}</dt>
            <dd class="font-mono text-xs text-text-muted">{{ voucher.journalEntryId }}</dd>
          </div>
          <div v-if="voucher.reversalJournalEntryId" class="flex justify-between border-b border-border pb-2">
            <dt class="text-text-muted">{{ t('expenses.detail.reversalJournal') }}</dt>
            <dd class="font-mono text-xs text-text-muted">{{ voucher.reversalJournalEntryId }}</dd>
          </div>
        </dl>
        <p v-if="voucher.notes" class="mt-3 border-t border-border pt-3 text-sm text-text">{{ voucher.notes }}</p>
      </AppCard>

      <DocumentTimeline document-type="ExpenseVoucher" :document-id="voucher.id" />
    </template>

    <AppModal v-model="reverseModal" :title="t('expenses.detail.reverseTitle')">
      <div class="space-y-4">
        <p class="text-sm text-text-muted">{{ t('expenses.detail.reverseIntro') }}</p>
        <FormField :label="t('expenses.detail.reverseDate')" required>
          <AppInput v-model="reverseDate" type="date" />
        </FormField>
        <FormField :label="t('expenses.detail.reverseReason')">
          <AppInput v-model="reverseReason" />
        </FormField>
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="reverseModal = false">{{ t('masterData.cancel') }}</AppButton>
        <AppButton variant="danger" @click="confirmReverse">{{ t('expenses.detail.reverse') }}</AppButton>
      </template>
    </AppModal>
  </div>
</template>
