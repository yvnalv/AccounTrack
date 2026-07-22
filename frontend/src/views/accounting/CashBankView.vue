<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { Landmark, Wallet, ArrowLeftRight, ArrowDownLeft, ArrowUpRight, Building2, Banknote } from 'lucide-vue-next'
import type { Component } from 'vue'
import { accountingApi, cashAccounts } from '@/lib/accounting'
import { accountOptionLabel } from '@/lib/coa'
import type { Locale } from '@/i18n'
import type { AccountRef } from '@/types/accounting'
import type { SelectOption } from '@/components/ui/types'
import AppButton from '@/components/ui/AppButton.vue'
import AppInput from '@/components/ui/AppInput.vue'
import AppSelect from '@/components/ui/AppSelect.vue'
import AppModal from '@/components/ui/AppModal.vue'
import FormField from '@/components/ui/FormField.vue'

type FlowKey = 'capital' | 'drawing' | 'transfer' | 'receive' | 'spend' | 'loanReceipt' | 'loanRepayment'

const { t, locale } = useI18n()
const loc = computed(() => locale.value as Locale)
const accounts = ref<AccountRef[]>([])
const banner = ref('')

const today = () => {
  const d = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

onMounted(async () => {
  accounts.value = await accountingApi.accounts()
})

const postable = computed(() => accounts.value.filter((a) => a.isActive && a.allowPosting))
const toOptions = (list: AccountRef[]): SelectOption[] => list.map((a) => ({ value: a.id, label: accountOptionLabel(a, loc.value) }))
const cashOptions = computed(() => toOptions(cashAccounts(accounts.value)))
const allOptions = computed(() => toOptions(postable.value))
const equityOptions = computed(() => toOptions(postable.value.filter((a) => a.type === 'Equity')))
const liabilityOptions = computed(() => toOptions(postable.value.filter((a) => a.type === 'Liability')))

const flows: { key: FlowKey; icon: Component }[] = [
  { key: 'capital', icon: Landmark },
  { key: 'drawing', icon: Wallet },
  { key: 'transfer', icon: ArrowLeftRight },
  { key: 'receive', icon: ArrowDownLeft },
  { key: 'spend', icon: ArrowUpRight },
  { key: 'loanReceipt', icon: Building2 },
  { key: 'loanRepayment', icon: Banknote },
]

const active = ref<FlowKey | null>(null)
const open = ref(false)
const saving = ref(false)
const error = ref('')

const form = reactive({
  date: today(),
  amount: '',
  principal: '',
  interest: '',
  memo: '',
  cashAccountId: '',
  fromAccountId: '',
  toAccountId: '',
  otherAccountId: '',
  interestAccountId: '',
})

function openFlow(key: FlowKey) {
  active.value = key
  Object.assign(form, {
    date: today(), amount: '', principal: '', interest: '', memo: '',
    cashAccountId: '', fromAccountId: '', toAccountId: '', otherAccountId: '', interestAccountId: '',
  })
  error.value = ''
  open.value = true
}

const num = (s: string) => parseFloat(s) || 0

const canSubmit = computed(() => {
  const k = active.value
  if (!k) return false
  if (k === 'transfer') return num(form.amount) > 0 && !!form.fromAccountId && !!form.toAccountId
  if (k === 'receive') return num(form.amount) > 0 && !!form.cashAccountId && !!form.otherAccountId
  if (k === 'spend') return num(form.amount) > 0 && !!form.cashAccountId && !!form.otherAccountId
  if (k === 'loanRepayment') return num(form.principal) > 0 && !!form.cashAccountId
  return num(form.amount) > 0 && !!form.cashAccountId
})

async function submit() {
  const k = active.value
  if (!k) return
  error.value = ''
  if (k === 'transfer' && form.fromAccountId === form.toAccountId) {
    error.value = t('accounting.cashBank.transfer.sameAccount')
    return
  }
  saving.value = true
  try {
    const memo = form.memo || null
    let res
    if (k === 'capital') {
      res = await accountingApi.capitalContribution({ date: form.date, amount: num(form.amount), cashAccountId: form.cashAccountId, equityAccountId: form.otherAccountId || null, memo })
    } else if (k === 'drawing') {
      res = await accountingApi.ownerDrawing({ date: form.date, amount: num(form.amount), cashAccountId: form.cashAccountId, drawingsAccountId: form.otherAccountId || null, memo })
    } else if (k === 'transfer') {
      res = await accountingApi.bankTransfer({ date: form.date, amount: num(form.amount), fromAccountId: form.fromAccountId, toAccountId: form.toAccountId, memo })
    } else if (k === 'receive') {
      res = await accountingApi.receiveMoney({ date: form.date, amount: num(form.amount), cashAccountId: form.cashAccountId, creditAccountId: form.otherAccountId, memo })
    } else if (k === 'spend') {
      res = await accountingApi.spendMoney({ date: form.date, amount: num(form.amount), cashAccountId: form.cashAccountId, debitAccountId: form.otherAccountId, memo })
    } else if (k === 'loanReceipt') {
      res = await accountingApi.loanReceipt({ date: form.date, amount: num(form.amount), cashAccountId: form.cashAccountId, loanAccountId: form.otherAccountId || null, memo })
    } else {
      res = await accountingApi.loanRepayment({ date: form.date, principal: num(form.principal), interest: num(form.interest), cashAccountId: form.cashAccountId, loanAccountId: form.otherAccountId || null, interestAccountId: form.interestAccountId || null, memo })
    }
    open.value = false
    banner.value = res.status === 'Pending' ? t('accounting.cashBank.pendingResult') : t('accounting.cashBank.postedResult')
  } catch (e) {
    error.value = e instanceof Error ? e.message : t('accounting.cashBank.failed')
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="space-y-4">
    <p class="text-sm text-text-muted">{{ t('accounting.cashBank.subtitle') }}</p>
    <p v-if="banner" class="rounded-control bg-positive/10 px-3 py-2 text-sm text-positive">{{ banner }}</p>

    <div class="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
      <button
        v-for="f in flows"
        :key="f.key"
        type="button"
        class="flex items-start gap-3 rounded-card border border-border bg-surface p-4 text-left transition-colors hover:border-accent hover:bg-surface-2"
        @click="openFlow(f.key)"
      >
        <span class="grid h-10 w-10 shrink-0 place-items-center rounded-control bg-accent/10 text-accent">
          <component :is="f.icon" :size="20" />
        </span>
        <span>
          <span class="block text-sm font-semibold text-text">{{ t(`accounting.cashBank.${f.key}.title`) }}</span>
          <span class="mt-0.5 block text-xs text-text-muted">{{ t(`accounting.cashBank.${f.key}.desc`) }}</span>
        </span>
      </button>
    </div>

    <AppModal v-model="open" :title="active ? t(`accounting.cashBank.${active}.title`) : ''">
      <div v-if="active" class="space-y-4">
        <div class="grid grid-cols-2 gap-4">
          <FormField :label="t('accounting.cashBank.date')" required>
            <AppInput v-model="form.date" type="date" />
          </FormField>
          <FormField v-if="active !== 'loanRepayment'" :label="t('accounting.cashBank.amount')" required>
            <AppInput v-model="form.amount" type="number" />
          </FormField>
        </div>

        <!-- Loan repayment: principal + interest -->
        <div v-if="active === 'loanRepayment'" class="grid grid-cols-2 gap-4">
          <FormField :label="t('accounting.cashBank.loanRepayment.principal')" required>
            <AppInput v-model="form.principal" type="number" />
          </FormField>
          <FormField :label="t('accounting.cashBank.loanRepayment.interest')">
            <AppInput v-model="form.interest" type="number" />
          </FormField>
        </div>

        <!-- Transfer: from / to -->
        <template v-if="active === 'transfer'">
          <FormField :label="t('accounting.cashBank.transfer.from')" required>
            <AppSelect v-model="form.fromAccountId" :options="cashOptions" :placeholder="t('accounting.cashBank.selectAccount')" />
          </FormField>
          <FormField :label="t('accounting.cashBank.transfer.to')" required>
            <AppSelect v-model="form.toAccountId" :options="cashOptions" :placeholder="t('accounting.cashBank.selectAccount')" />
          </FormField>
        </template>

        <!-- All non-transfer flows have a cash/bank account -->
        <FormField v-else :label="t('accounting.cashBank.cashAccount')" required>
          <AppSelect v-model="form.cashAccountId" :options="cashOptions" :placeholder="t('accounting.cashBank.selectAccount')" />
        </FormField>

        <!-- Second account, per flow -->
        <FormField v-if="active === 'capital'" :label="t('accounting.cashBank.capital.equityAccount')">
          <AppSelect v-model="form.otherAccountId" :options="equityOptions" :placeholder="t('accounting.cashBank.optionalDefault')" />
          <p class="mt-1 text-xs text-text-muted">{{ t('accounting.cashBank.capital.equityHint') }}</p>
        </FormField>
        <FormField v-else-if="active === 'drawing'" :label="t('accounting.cashBank.drawing.drawingsAccount')">
          <AppSelect v-model="form.otherAccountId" :options="equityOptions" :placeholder="t('accounting.cashBank.optionalDefault')" />
        </FormField>
        <FormField v-else-if="active === 'receive'" :label="t('accounting.cashBank.receive.creditAccount')" required>
          <AppSelect v-model="form.otherAccountId" :options="allOptions" :placeholder="t('accounting.cashBank.account')" />
        </FormField>
        <FormField v-else-if="active === 'spend'" :label="t('accounting.cashBank.spend.debitAccount')" required>
          <AppSelect v-model="form.otherAccountId" :options="allOptions" :placeholder="t('accounting.cashBank.account')" />
        </FormField>
        <FormField v-else-if="active === 'loanReceipt'" :label="t('accounting.cashBank.loanReceipt.loanAccount')">
          <AppSelect v-model="form.otherAccountId" :options="liabilityOptions" :placeholder="t('accounting.cashBank.optionalDefault')" />
        </FormField>
        <template v-else-if="active === 'loanRepayment'">
          <FormField :label="t('accounting.cashBank.loanRepayment.loanAccount')">
            <AppSelect v-model="form.otherAccountId" :options="liabilityOptions" :placeholder="t('accounting.cashBank.optionalDefault')" />
          </FormField>
          <FormField v-if="num(form.interest) > 0" :label="t('accounting.cashBank.loanRepayment.interestAccount')" required>
            <AppSelect v-model="form.interestAccountId" :options="allOptions" :placeholder="t('accounting.cashBank.account')" />
          </FormField>
        </template>

        <FormField :label="t('accounting.cashBank.memo')">
          <AppInput v-model="form.memo" />
        </FormField>

        <p v-if="error" class="text-sm text-negative">{{ error }}</p>
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="open = false">{{ t('masterData.cancel') }}</AppButton>
        <AppButton :disabled="saving || !canSubmit" @click="submit">
          {{ saving ? t('accounting.cashBank.saving') : t('accounting.cashBank.submit') }}
        </AppButton>
      </template>
    </AppModal>
  </div>
</template>
