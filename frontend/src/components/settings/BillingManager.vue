<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { Check, RefreshCw, CreditCard } from 'lucide-vue-next'
import { billingApi } from '@/lib/billing'
import { apiErrorMessage } from '@/lib/api'
import { formatMoney } from '@/lib/format'
import { useAuthStore } from '@/stores/auth'
import type { BillingInvoice, Plan, Subscription } from '@/types/billing'
import AppButton from '@/components/ui/AppButton.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'

const { t, locale } = useI18n()
const auth = useAuthStore()

const canManage = computed(() => auth.has('Billing.Manage'))

const subscription = ref<Subscription | null>(null)
const plans = ref<Plan[]>([])
const invoices = ref<BillingInvoice[]>([])
const loading = ref(true)
const error = ref('')
const busy = ref('') // planCode being acted on, or 'pay'

async function load() {
  loading.value = true
  error.value = ''
  try {
    const [sub, pl, inv] = await Promise.all([
      billingApi.subscription(),
      billingApi.plans(),
      billingApi.invoices(),
    ])
    subscription.value = sub
    plans.value = pl
    invoices.value = inv
  } catch (e) {
    error.value = apiErrorMessage(e, t('billing.loadFailed'))
  } finally {
    loading.value = false
  }
}
onMounted(load)

// --- formatting helpers ---
const df = computed(() => new Intl.DateTimeFormat(locale.value, { dateStyle: 'medium' }))
const fmtDate = (iso: string | null) => (iso ? df.value.format(new Date(iso)) : '—')
const price = (minor: number, currency: string) => formatMoney(minor, currency)

const intervalLabel = (interval: string) =>
  interval === 'Annual' ? t('billing.perYear') : t('billing.perMonth')

const statusTone = (status: string) =>
  ({
    Active: 'positive',
    Trialing: 'info',
    PastDue: 'warning',
    Canceled: 'neutral',
    Unpaid: 'negative',
    Expired: 'negative',
  })[status] as 'positive' | 'info' | 'warning' | 'neutral' | 'negative' | undefined

const statusLabel = (status: string) => t(`billing.status.${status}`)

// Known feature flags → readable labels (unknown keys fall back to the key).
const featureLabel = (key: string) => {
  const known: Record<string, string> = {
    approvals: t('billing.features.approvals'),
    multiWarehouse: t('billing.features.multiWarehouse'),
    exports: t('billing.features.exports'),
    api: t('billing.features.api'),
    prioritySupport: t('billing.features.prioritySupport'),
  }
  return known[key] ?? key
}
function planFeatures(p: Plan): string[] {
  try {
    const obj = JSON.parse(p.featuresJson) as Record<string, boolean>
    return Object.entries(obj)
      .filter(([, on]) => on)
      .map(([k]) => featureLabel(k))
  } catch {
    return []
  }
}
function seatsLabel(p: Plan): string {
  return t('billing.seats', { n: p.includedSeats })
}
function companiesLabel(p: Plan): string {
  return p.maxCompanies == null
    ? t('billing.unlimitedCompanies')
    : t('billing.companies', { n: p.maxCompanies })
}

const currentPlanId = computed(() => subscription.value?.planId ?? null)
const hasSubscription = computed(() => subscription.value !== null)

// Monthly/Annual toggle: group the (tier × interval) plans into one card per tier, showing the plan
// for the selected interval — the standard pricing-page pattern.
const selectedInterval = ref<'Monthly' | 'Annual'>('Monthly')

interface PlanCard {
  tier: string
  name: string
  plan: Plan
}
const cards = computed<PlanCard[]>(() => {
  const byTier = new Map<string, { name: string; plans: Record<string, Plan> }>()
  for (const p of plans.value) {
    const tier = p.code.split('-')[0]
    const name = p.name.replace(/\s*\((?:Monthly|Annual)\)\s*$/i, '').trim()
    const entry = byTier.get(tier) ?? { name, plans: {} as Record<string, Plan> }
    entry.plans[p.interval] = p
    byTier.set(tier, entry)
  }
  return [...byTier.entries()]
    .map(([tier, e]) => ({ tier, name: e.name, plan: e.plans[selectedInterval.value] ?? Object.values(e.plans)[0] }))
    .filter((c): c is PlanCard => !!c.plan)
    .sort((a, b) => a.plan.basePriceMinor - b.plan.basePriceMinor)
})

// Show the interval toggle only when at least one plan actually offers an annual option.
const hasAnnualOption = computed(() => plans.value.some((p) => p.interval === 'Annual'))

// Classify each plan card's call-to-action relative to the current subscription, so the buttons read
// as Upgrade / Downgrade / Switch billing cycle rather than a generic action.
const currentPlan = computed(() => plans.value.find((p) => p.id === currentPlanId.value) ?? null)
const currentTier = computed(() => currentPlan.value?.code.split('-')[0] ?? null)
const currentTierIndex = computed(() =>
  currentTier.value ? cards.value.findIndex((c) => c.tier === currentTier.value) : -1,
)

type Cta = 'upgrade' | 'downgrade' | 'switchCycle'
function ctaFor(c: PlanCard, index: number): Cta | null {
  if (!hasSubscription.value || c.plan.id === currentPlanId.value) return null
  if (c.tier === currentTier.value) return 'switchCycle' // same tier, different billing interval
  if (currentTierIndex.value >= 0 && index < currentTierIndex.value) return 'downgrade'
  return 'upgrade'
}

// The past-due / locked banner (SUBSCRIPTION_BILLING.md §7).
const banner = computed(() => {
  const s = subscription.value?.status
  if (s === 'PastDue') return { tone: 'warning', text: t('billing.bannerPastDue') }
  if (s === 'Unpaid' || s === 'Expired') return { tone: 'negative', text: t('billing.bannerLocked') }
  return null
})

// --- actions ---
async function startTrial(p: Plan) {
  if (!canManage.value) return
  busy.value = p.code
  error.value = ''
  try {
    await billingApi.startTrial(p.code)
    await load()
  } catch (e) {
    error.value = apiErrorMessage(e, t('billing.actionFailed'))
  } finally {
    busy.value = ''
  }
}

// Checkout → redirect the browser to Xendit's hosted pay page. Passing a planCode upgrades/downgrades
// to that plan first. The subscription activates via the webhook (source of truth); on return we reload.
async function pay(planCode?: string) {
  if (!canManage.value) return
  busy.value = planCode ?? 'pay'
  error.value = ''
  try {
    const checkout = await billingApi.checkout(planCode)
    window.location.href = checkout.payUrl
  } catch (e) {
    error.value = apiErrorMessage(e, t('billing.actionFailed'))
    busy.value = ''
  }
}
</script>

<template>
  <div class="space-y-6">
    <div class="flex items-center justify-between">
      <div>
        <h2 class="text-base font-semibold text-text">{{ t('billing.title') }}</h2>
        <p class="text-sm text-text-muted">{{ t('billing.subtitle') }}</p>
      </div>
      <AppButton variant="ghost" size="sm" :disabled="loading" @click="load">
        <RefreshCw :size="15" :class="{ 'animate-spin': loading }" />
        {{ t('common.refresh') }}
      </AppButton>
    </div>

    <p v-if="error" class="rounded-control bg-negative-soft px-3 py-2 text-sm text-negative">{{ error }}</p>

    <!-- Access banner (past-due / locked) -->
    <p
      v-if="banner"
      class="rounded-card border px-4 py-3 text-sm"
      :class="banner.tone === 'warning'
        ? 'border-warning/30 bg-warning-soft text-warning'
        : 'border-negative/30 bg-negative-soft text-negative'"
    >
      {{ banner.text }}
    </p>

    <p v-if="loading" class="text-sm text-text-muted">{{ t('common.loading') }}</p>

    <template v-else>
      <!-- Current subscription -->
      <section
        v-if="subscription"
        class="rounded-card border border-border bg-surface p-5"
      >
        <div class="flex flex-wrap items-start justify-between gap-4">
          <div>
            <div class="flex items-center gap-2">
              <h3 class="text-lg font-semibold text-text">{{ subscription.planName }}</h3>
              <StatusBadge :label="statusLabel(subscription.status)" :tone="statusTone(subscription.status)" />
            </div>
            <dl class="mt-3 grid grid-cols-2 gap-x-8 gap-y-1 text-sm sm:grid-cols-3">
              <div v-if="subscription.status === 'Trialing' && subscription.trialEndsAt">
                <dt class="text-text-muted">{{ t('billing.trialEnds') }}</dt>
                <dd class="text-text">{{ fmtDate(subscription.trialEndsAt) }}</dd>
              </div>
              <div v-if="subscription.currentPeriodEnd">
                <dt class="text-text-muted">{{ t('billing.renews') }}</dt>
                <dd class="text-text">{{ fmtDate(subscription.currentPeriodEnd) }}</dd>
              </div>
              <div>
                <dt class="text-text-muted">{{ t('billing.billingCycle') }}</dt>
                <dd class="text-text">{{ t(`billing.interval.${subscription.interval}`) }}</dd>
              </div>
            </dl>
          </div>
          <AppButton
            v-if="canManage && subscription.status !== 'Active'"
            :disabled="busy === 'pay'"
            @click="pay"
          >
            <CreditCard :size="16" />
            {{ busy === 'pay' ? t('billing.redirecting') : t('billing.payNow') }}
          </AppButton>
        </div>
        <p v-if="!canManage" class="mt-3 text-xs text-text-muted">{{ t('billing.viewOnlyHint') }}</p>
      </section>

      <!-- No subscription yet -->
      <section v-else class="rounded-card border border-dashed border-border bg-surface p-5 text-center">
        <p class="text-sm text-text-muted">{{ t('billing.noSubscription') }}</p>
      </section>

      <!-- Plans -->
      <section>
        <div class="mb-4 flex flex-wrap items-center justify-between gap-3">
          <h3 class="text-sm font-semibold uppercase tracking-wide text-text-muted">
            {{ hasSubscription ? t('billing.availablePlans') : t('billing.choosePlan') }}
          </h3>

          <!-- Monthly / Annual toggle -->
          <div
            v-if="hasAnnualOption"
            class="inline-flex items-center gap-0.5 rounded-control border border-border bg-surface-2 p-0.5 text-sm"
            role="tablist"
          >
            <button
              v-for="opt in (['Monthly', 'Annual'] as const)"
              :key="opt"
              type="button"
              role="tab"
              :aria-selected="selectedInterval === opt"
              class="rounded px-3 py-1 font-medium transition-colors"
              :class="selectedInterval === opt
                ? 'bg-surface text-text shadow-sm'
                : 'text-text-muted hover:text-text'"
              @click="selectedInterval = opt"
            >
              {{ t(`billing.interval.${opt}`) }}
              <span v-if="opt === 'Annual'" class="ml-1 text-[11px] font-normal text-positive">
                {{ t('billing.saveHint') }}
              </span>
            </button>
          </div>
        </div>

        <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <div
            v-for="(c, i) in cards"
            :key="c.tier"
            class="flex flex-col rounded-card border bg-surface p-5 transition-colors"
            :class="c.plan.id === currentPlanId ? 'border-accent ring-1 ring-accent' : 'border-border'"
          >
            <div class="flex items-center justify-between">
              <h4 class="font-semibold text-text">{{ c.name }}</h4>
              <span
                v-if="c.plan.id === currentPlanId"
                class="rounded-full bg-accent-soft px-2 py-0.5 text-[11px] font-medium text-accent"
              >
                {{ t('billing.current') }}
              </span>
            </div>

            <p class="mt-2 flex items-baseline gap-1">
              <span class="text-2xl font-semibold text-text tnum">{{ price(c.plan.basePriceMinor, c.plan.currency) }}</span>
              <span class="text-sm text-text-muted">{{ intervalLabel(c.plan.interval) }}</span>
            </p>

            <ul class="mt-4 space-y-1.5 text-sm text-text">
              <li class="flex items-center gap-2">
                <Check :size="15" class="shrink-0 text-positive" /> {{ seatsLabel(c.plan) }}
              </li>
              <li class="flex items-center gap-2">
                <Check :size="15" class="shrink-0 text-positive" /> {{ companiesLabel(c.plan) }}
              </li>
              <li v-for="f in planFeatures(c.plan)" :key="f" class="flex items-center gap-2">
                <Check :size="15" class="shrink-0 text-positive" /> {{ f }}
              </li>
            </ul>

            <div class="mt-auto pt-5">
              <!-- No subscription yet: start a trial on this plan. -->
              <AppButton
                v-if="!hasSubscription"
                block
                :disabled="!canManage || busy === c.plan.code"
                @click="startTrial(c.plan)"
              >
                {{ busy === c.plan.code ? t('common.loading') : t('billing.startTrial') }}
              </AppButton>

              <!-- The plan the tenant is on. -->
              <p v-else-if="c.plan.id === currentPlanId" class="text-center text-xs font-medium text-text-muted">
                {{ t('billing.yourPlan') }}
              </p>

              <!-- Another plan: upgrade / downgrade / switch billing cycle → checkout to that plan. -->
              <AppButton
                v-else
                variant="secondary"
                block
                :disabled="!canManage || busy === c.plan.code"
                @click="pay(c.plan.code)"
              >
                {{ busy === c.plan.code ? t('billing.redirecting') : t(`billing.${ctaFor(c, i) ?? 'upgrade'}`) }}
              </AppButton>
            </div>
          </div>
        </div>
        <p v-if="!hasSubscription && !canManage" class="mt-2 text-xs text-text-muted">
          {{ t('billing.viewOnlyHint') }}
        </p>
      </section>

      <!-- Invoice history -->
      <section v-if="invoices.length > 0">
        <h3 class="mb-3 text-sm font-semibold uppercase tracking-wide text-text-muted">
          {{ t('billing.history') }}
        </h3>
        <div class="overflow-x-auto rounded-card border border-border">
          <table class="w-full text-sm">
            <thead>
              <tr class="border-b border-border text-left text-text-muted">
                <th class="px-4 py-2 font-medium">{{ t('billing.invoiceNo') }}</th>
                <th class="px-4 py-2 font-medium">{{ t('billing.period') }}</th>
                <th class="px-4 py-2 text-right font-medium">{{ t('billing.amount') }}</th>
                <th class="px-4 py-2 font-medium">{{ t('billing.invoiceStatus') }}</th>
                <th class="px-4 py-2 font-medium">{{ t('billing.paidOn') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="inv in invoices" :key="inv.id" class="border-b border-border last:border-0">
                <td class="px-4 py-2 text-text">{{ inv.number }}</td>
                <td class="px-4 py-2 text-text-muted">
                  {{ fmtDate(inv.periodStart) }} – {{ fmtDate(inv.periodEnd) }}
                </td>
                <td class="px-4 py-2 text-right text-text tnum">{{ price(inv.totalMinor, inv.currency) }}</td>
                <td class="px-4 py-2">
                  <StatusBadge :label="t(`billing.invoiceStatuses.${inv.status}`)" :status="inv.status" />
                </td>
                <td class="px-4 py-2 text-text-muted">{{ fmtDate(inv.paidAt) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </template>
  </div>
</template>
