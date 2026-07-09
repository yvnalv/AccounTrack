<script setup lang="ts">
import type { Component } from 'vue'
import { computed, defineAsyncComponent, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink } from 'vue-router'
import type { EChartsOption } from 'echarts'
import { ShoppingCart, Truck, Boxes, Receipt, Package, CheckSquare, ArrowRight } from 'lucide-vue-next'
import { http, unwrap } from '@/lib/api'
import { formatMoney, formatMoneyShort } from '@/lib/format'
import type { DashboardAging, DashboardSummary } from '@/types/api'
import { useThemeStore } from '@/stores/theme'
import { useAuthStore } from '@/stores/auth'
import AppCard from '@/components/ui/AppCard.vue'
import StatTile from '@/components/ui/StatTile.vue'

// ECharts (~500 KB) is loaded on demand as its own chunk, so it no longer bloats the dashboard route
// chunk; the KPI cards render immediately while the charts hydrate into their fixed-height containers.
const AppChart = defineAsyncComponent(() => import('@/components/ui/AppChart.vue'))

const { t } = useI18n()
const theme = useThemeStore()
const auth = useAuthStore()

const summary = ref<DashboardSummary | null>(null)
const loading = ref(true)
const error = ref('')

// The finance summary requires Accounting.View. Roles without it (Sales, Purchasing, Warehouse) get a
// role-aware landing instead of a hard error; richer per-role KPIs are a follow-up (PR2).
const canViewFinance = computed(() => auth.has('Accounting.View'))

interface QuickLink { to: { name: string }; label: string; icon: Component; permission?: string }
const quickLinks = computed<QuickLink[]>(() =>
  (
    [
      { to: { name: 'sales' }, label: t('nav.sales'), icon: ShoppingCart, permission: 'Sales.View' },
      { to: { name: 'purchasing' }, label: t('nav.purchasing'), icon: Truck, permission: 'Purchasing.View' },
      { to: { name: 'inventory' }, label: t('nav.inventory'), icon: Boxes, permission: 'Inventory.View' },
      { to: { name: 'expenses' }, label: t('nav.expenses'), icon: Receipt, permission: 'Expenses.View' },
      { to: { name: 'masterDataProducts' }, label: t('nav.masterData'), icon: Package, permission: 'MasterData.View' },
      { to: { name: 'approvals' }, label: t('nav.approvals'), icon: CheckSquare },
    ] as QuickLink[]
  ).filter((l) => !l.permission || auth.has(l.permission)),
)

async function load() {
  if (!canViewFinance.value) {
    loading.value = false
    return
  }
  loading.value = true
  error.value = ''
  try {
    summary.value = await unwrap<DashboardSummary>(http.get('/dashboard/summary'))
  } catch {
    error.value = t('dashboard.loadError')
  } finally {
    loading.value = false
  }
}
onMounted(load)

const currency = computed(() => summary.value?.currency ?? 'IDR')

function cssVar(name: string, fallback: string): string {
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim() || fallback
}

// Brand-led, color-blind-friendly categorical palette for the expense donut.
const PALETTE = ['#007E6E', '#3B82F6', '#F59E0B', '#A855F7', '#EF4444', '#14B8A6', '#64748B', '#EC4899']
const AGING_COLORS = ['#10B981', '#F59E0B', '#FB923C', '#F87171', '#EF4444']

function compact(n: number): string {
  const abs = Math.abs(n)
  if (abs >= 1e9) return (n / 1e9).toFixed(1) + 'B'
  if (abs >= 1e6) return (n / 1e6).toFixed(1) + 'M'
  if (abs >= 1e3) return (n / 1e3).toFixed(0) + 'K'
  return String(Math.round(n))
}

function pctDelta(now: number, prev: number): number | undefined {
  if (prev === 0) return undefined
  return Math.round(((now - prev) / Math.abs(prev)) * 100)
}

const revenueDelta = computed(() =>
  summary.value ? pctDelta(summary.value.revenueThisMonth, summary.value.revenuePrevMonth) : undefined,
)

const overdueArHint = computed(() =>
  summary.value && summary.value.overdueReceivableCount > 0
    ? t('dashboard.overdueCount', { count: summary.value.overdueReceivableCount })
    : undefined,
)
const overdueApHint = computed(() =>
  summary.value && summary.value.overduePayableCount > 0
    ? t('dashboard.overdueCount', { count: summary.value.overduePayableCount })
    : undefined,
)

function baseAxis() {
  const muted = cssVar('--text-muted', '#6b7280')
  const border = cssVar('--border', '#e7e9ec')
  return { muted, border }
}

function tooltipBox() {
  return {
    backgroundColor: '#15171A',
    borderWidth: 0,
    textStyle: { color: '#fff', fontSize: 12 },
  }
}

// 6-month revenue / expense bars + profit line.
const trendOption = computed<EChartsOption>(() => {
  void theme.theme
  const s = summary.value
  const { muted, border } = baseAxis()
  const accent = cssVar('--accent', '#007E6E')
  const months = (s?.monthlyTrend ?? []).map((p) => p.month.slice(2)) // "26-06"
  return {
    grid: { top: 30, right: 16, bottom: 28, left: 52 },
    legend: { top: 0, textStyle: { color: muted }, itemHeight: 8, itemWidth: 8, icon: 'roundRect' },
    tooltip: {
      trigger: 'axis',
      ...tooltipBox(),
      valueFormatter: (v) => formatMoney(Number(v), currency.value),
    },
    xAxis: {
      type: 'category',
      data: months,
      axisLine: { lineStyle: { color: border } },
      axisTick: { show: false },
      axisLabel: { color: muted },
    },
    yAxis: {
      type: 'value',
      splitLine: { lineStyle: { color: border, type: 'dashed' } },
      axisLabel: { color: muted, formatter: (v: number) => compact(v) },
    },
    series: [
      { name: t('dashboard.revenue'), type: 'bar', barMaxWidth: 22, itemStyle: { color: accent, borderRadius: [4, 4, 0, 0] }, data: (s?.monthlyTrend ?? []).map((p) => p.revenue) },
      { name: t('dashboard.expense'), type: 'bar', barMaxWidth: 22, itemStyle: { color: '#EF4444', borderRadius: [4, 4, 0, 0] }, data: (s?.monthlyTrend ?? []).map((p) => p.expense) },
      { name: t('dashboard.profit'), type: 'line', smooth: true, symbolSize: 6, lineStyle: { width: 2, color: '#3B82F6' }, itemStyle: { color: '#3B82F6' }, data: (s?.monthlyTrend ?? []).map((p) => p.profit) },
    ],
  }
})

const hasExpense = computed(() => (summary.value?.expenseByCategory.length ?? 0) > 0)
const expenseOption = computed<EChartsOption>(() => {
  void theme.theme
  const { muted } = baseAxis()
  const data = (summary.value?.expenseByCategory ?? []).map((c, i) => ({
    name: c.name,
    value: c.amount,
    itemStyle: { color: PALETTE[i % PALETTE.length] },
  }))
  return {
    tooltip: { trigger: 'item', ...tooltipBox(), valueFormatter: (v) => formatMoney(Number(v), currency.value) },
    legend: { type: 'scroll', orient: 'vertical', right: 0, top: 'middle', textStyle: { color: muted, fontSize: 11 }, itemHeight: 8, itemWidth: 8, icon: 'circle' },
    series: [
      {
        type: 'pie',
        radius: ['52%', '74%'],
        center: ['32%', '50%'],
        avoidLabelOverlap: true,
        itemStyle: { borderColor: cssVar('--surface', '#fff'), borderWidth: 2 },
        label: { show: false },
        data,
      },
    ],
  }
})

function agingOption(aging: DashboardAging | undefined): EChartsOption {
  void theme.theme
  const { muted, border } = baseAxis()
  const a = aging ?? { current: 0, days1To30: 0, days31To60: 0, days61To90: 0, days90Plus: 0 }
  const cats = [t('dashboard.aging.current'), t('dashboard.aging.d30'), t('dashboard.aging.d60'), t('dashboard.aging.d90'), t('dashboard.aging.d90plus')]
  const vals = [a.current, a.days1To30, a.days31To60, a.days61To90, a.days90Plus]
  return {
    grid: { top: 16, right: 16, bottom: 28, left: 52 },
    tooltip: { trigger: 'axis', ...tooltipBox(), valueFormatter: (v) => formatMoney(Number(v), currency.value) },
    xAxis: { type: 'category', data: cats, axisLine: { lineStyle: { color: border } }, axisTick: { show: false }, axisLabel: { color: muted, fontSize: 11 } },
    yAxis: { type: 'value', splitLine: { lineStyle: { color: border, type: 'dashed' } }, axisLabel: { color: muted, formatter: (v: number) => compact(v) } },
    series: [
      {
        type: 'bar',
        barMaxWidth: 36,
        itemStyle: { borderRadius: [4, 4, 0, 0] },
        data: vals.map((v, i) => ({ value: v, itemStyle: { color: AGING_COLORS[i] } })),
      },
    ],
  }
}
const arAgingOption = computed(() => agingOption(summary.value?.arAging))
const apAgingOption = computed(() => agingOption(summary.value?.apAging))

function barWidth(amount: number, list: { amount: number }[]): string {
  const max = Math.max(...list.map((x) => x.amount), 1)
  return `${Math.max(4, (amount / max) * 100)}%`
}
</script>

<template>
  <div class="space-y-6">
    <p v-if="loading" class="text-sm text-text-muted">{{ t('common.loading') }}</p>

    <!-- Role-aware landing for users without finance access (no hard error). -->
    <div v-else-if="!canViewFinance" class="space-y-5">
      <AppCard>
        <h2 class="text-base font-semibold text-text">{{ t('dashboard.welcome', { name: auth.user?.fullName ?? '' }) }}</h2>
        <p class="mt-1 text-sm text-text-muted">{{ t('dashboard.welcomeBody') }}</p>
      </AppCard>
      <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <RouterLink
          v-for="link in quickLinks"
          :key="link.label"
          :to="link.to"
          class="group flex items-center gap-3 rounded-card border border-border bg-surface p-4 transition-colors hover:border-accent"
        >
          <div class="grid h-10 w-10 shrink-0 place-items-center rounded-control bg-accent-soft text-accent">
            <component :is="link.icon" :size="20" />
          </div>
          <span class="flex-1 text-sm font-medium text-text">{{ link.label }}</span>
          <ArrowRight :size="16" class="text-text-muted transition-transform group-hover:translate-x-0.5" />
        </RouterLink>
      </div>
    </div>

    <div v-else-if="error" class="text-sm text-negative">
      {{ error }}
      <button class="ml-2 underline" @click="load">{{ t('common.retry') }}</button>
    </div>

    <template v-else-if="summary">
      <!-- Insight tiles -->
      <div class="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-5">
        <StatTile :label="t('dashboard.cashAndBank')" :value="formatMoneyShort(summary.cashAndBank, currency)" />
        <StatTile :label="t('dashboard.accountsReceivable')" :value="formatMoneyShort(summary.accountsReceivable, currency)" :hint="overdueArHint" hint-tone="negative" />
        <StatTile :label="t('dashboard.accountsPayable')" :value="formatMoneyShort(summary.accountsPayable, currency)" :hint="overdueApHint" hint-tone="negative" />
        <StatTile :label="t('dashboard.revenue')" :value="formatMoneyShort(summary.revenueThisMonth, currency)" :delta="revenueDelta" :hint="t('dashboard.vsLastMonth')" />
        <StatTile :label="t('dashboard.netProfitMonth')" :value="formatMoneyShort(summary.netProfitThisMonth, currency)" :hint-tone="summary.netProfitThisMonth >= 0 ? 'positive' : 'negative'" />
      </div>

      <!-- Trend + expense breakdown -->
      <div class="grid grid-cols-1 gap-5 lg:grid-cols-3">
        <AppCard :title="t('dashboard.trend')" class="lg:col-span-2">
          <div class="h-[300px] w-full">
            <AppChart :option="trendOption" />
          </div>
        </AppCard>
        <AppCard :title="t('dashboard.expenseBreakdown')">
          <div v-if="hasExpense" class="h-[300px] w-full">
            <AppChart :option="expenseOption" />
          </div>
          <p v-else class="grid h-[300px] place-items-center text-sm text-text-muted">{{ t('dashboard.noData') }}</p>
        </AppCard>
      </div>

      <!-- Aging -->
      <div class="grid grid-cols-1 gap-5 lg:grid-cols-2">
        <AppCard :title="t('dashboard.receivableAging')">
          <div class="h-[260px] w-full">
            <AppChart :option="arAgingOption" />
          </div>
        </AppCard>
        <AppCard :title="t('dashboard.payableAging')">
          <div class="h-[260px] w-full">
            <AppChart :option="apAgingOption" />
          </div>
        </AppCard>
      </div>

      <!-- Top debtors / creditors -->
      <div class="grid grid-cols-1 gap-5 lg:grid-cols-2">
        <AppCard :title="t('dashboard.topReceivables')">
          <p v-if="summary.topReceivables.length === 0" class="py-6 text-center text-sm text-text-muted">{{ t('dashboard.noData') }}</p>
          <ul v-else class="space-y-3">
            <li v-for="p in summary.topReceivables" :key="p.name">
              <div class="flex items-center justify-between text-sm">
                <span class="truncate text-text">{{ p.name }}</span>
                <span class="tnum text-text-muted">{{ formatMoneyShort(p.amount, currency) }}</span>
              </div>
              <div class="mt-1 h-1.5 w-full rounded-full bg-surface-2">
                <div class="h-1.5 rounded-full bg-accent" :style="{ width: barWidth(p.amount, summary.topReceivables) }" />
              </div>
            </li>
          </ul>
        </AppCard>
        <AppCard :title="t('dashboard.topPayables')">
          <p v-if="summary.topPayables.length === 0" class="py-6 text-center text-sm text-text-muted">{{ t('dashboard.noData') }}</p>
          <ul v-else class="space-y-3">
            <li v-for="p in summary.topPayables" :key="p.name">
              <div class="flex items-center justify-between text-sm">
                <span class="truncate text-text">{{ p.name }}</span>
                <span class="tnum text-text-muted">{{ formatMoneyShort(p.amount, currency) }}</span>
              </div>
              <div class="mt-1 h-1.5 w-full rounded-full bg-surface-2">
                <div class="h-1.5 rounded-full" :style="{ width: barWidth(p.amount, summary.topPayables), background: '#EF4444' }" />
              </div>
            </li>
          </ul>
        </AppCard>
      </div>
    </template>
  </div>
</template>
