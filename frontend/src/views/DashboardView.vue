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
import { salesApi } from '@/lib/sales'
import { purchasingApi } from '@/lib/purchasing'
import { inventoryApi } from '@/lib/inventory'
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

// Two dashboards: users with Accounting.View (Administrator, Accountant) get the deep financial view;
// everyone else gets a general, role-based operational view composed of the sections their permissions
// allow (Sales / Purchasing / Inventory). A user with none of those falls back to quick links.
const canViewFinance = computed(() => auth.has('Accounting.View'))
const canSeeSales = computed(() => auth.has('Sales.View'))
const canSeePurchasing = computed(() => auth.has('Purchasing.View'))
const canSeeInventory = computed(() => auth.has('Inventory.View'))
const hasOperational = computed(() => canSeeSales.value || canSeePurchasing.value || canSeeInventory.value)

interface Kpi { label: string; value: string; hint?: string; tone?: 'positive' | 'negative' }
const salesKpis = ref<Kpi[] | null>(null)
const purchasingKpis = ref<Kpi[] | null>(null)
const inventoryKpis = ref<Kpi[] | null>(null)
const opsCurrency = ref('IDR')

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

function inThisMonth(dateIso: string): boolean {
  const d = new Date(dateIso)
  const now = new Date()
  return d.getFullYear() === now.getFullYear() && d.getMonth() === now.getMonth()
}

async function loadSalesSection() {
  try {
    const orders = await salesApi.list()
    const open = new Set(['Draft', 'PendingApproval', 'Approved', 'PartiallyDelivered'])
    const toDeliver = new Set(['Approved', 'PartiallyDelivered'])
    const monthly = orders.filter((o) => inThisMonth(o.orderDate))
    salesKpis.value = [
      { label: t('dashboard.ops.openOrders'), value: String(orders.filter((o) => open.has(o.status)).length) },
      { label: t('dashboard.ops.ordersThisMonth'), value: String(monthly.length) },
      { label: t('dashboard.ops.salesThisMonth'), value: formatMoneyShort(monthly.reduce((s, o) => s + o.grandTotal, 0), opsCurrency.value) },
      { label: t('dashboard.ops.toDeliver'), value: String(orders.filter((o) => toDeliver.has(o.status)).length), tone: 'negative' },
    ]
  } catch {
    salesKpis.value = []
  }
}

async function loadPurchasingSection() {
  try {
    const orders = await purchasingApi.list()
    const open = new Set(['Draft', 'PendingApproval', 'Approved', 'PartiallyReceived'])
    const toReceive = new Set(['Approved', 'PartiallyReceived'])
    const monthly = orders.filter((o) => inThisMonth(o.orderDate))
    purchasingKpis.value = [
      { label: t('dashboard.ops.openPos'), value: String(orders.filter((o) => open.has(o.status)).length) },
      { label: t('dashboard.ops.posThisMonth'), value: String(monthly.length) },
      { label: t('dashboard.ops.purchasesThisMonth'), value: formatMoneyShort(monthly.reduce((s, o) => s + o.grandTotal, 0), opsCurrency.value) },
      { label: t('dashboard.ops.toReceive'), value: String(orders.filter((o) => toReceive.has(o.status)).length), tone: 'negative' },
    ]
  } catch {
    purchasingKpis.value = []
  }
}

async function loadInventorySection() {
  try {
    const [valuation, onHand] = await Promise.all([inventoryApi.valuation(), inventoryApi.onHand()])
    opsCurrency.value = valuation.currency || opsCurrency.value
    const outOfStock = onHand.filter((r) => r.onHandQty <= 0).length
    inventoryKpis.value = [
      { label: t('dashboard.ops.stockValue'), value: formatMoneyShort(valuation.totalValue, valuation.currency) },
      { label: t('dashboard.ops.skus'), value: String(valuation.rows.length) },
      { label: t('dashboard.ops.outOfStock'), value: String(outOfStock), tone: outOfStock > 0 ? 'negative' : undefined },
    ]
  } catch {
    inventoryKpis.value = []
  }
}

async function loadOperational() {
  const tasks: Promise<void>[] = []
  // Inventory first so its currency is available to the money tiles of the other sections.
  if (canSeeInventory.value) tasks.push(loadInventorySection())
  if (canSeeSales.value) tasks.push(loadSalesSection())
  if (canSeePurchasing.value) tasks.push(loadPurchasingSection())
  await Promise.allSettled(tasks)
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    if (canViewFinance.value) {
      summary.value = await unwrap<DashboardSummary>(http.get('/dashboard/summary'))
    } else if (hasOperational.value) {
      await loadOperational()
    }
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

    <!-- General, role-based operational dashboard for users without finance access (no hard error). -->
    <div v-else-if="!canViewFinance" class="space-y-6">
      <div>
        <h2 class="text-base font-semibold text-text">{{ t('dashboard.welcome', { name: auth.user?.fullName ?? '' }) }}</h2>
        <p class="mt-1 text-sm text-text-muted">{{ t('dashboard.welcomeBody') }}</p>
      </div>

      <!-- Sales -->
      <section v-if="canSeeSales && salesKpis && salesKpis.length" class="space-y-3">
        <div class="flex items-center gap-2">
          <ShoppingCart :size="16" class="text-accent" />
          <h3 class="text-sm font-semibold text-text">{{ t('nav.sales') }}</h3>
          <RouterLink :to="{ name: 'sales' }" class="ml-auto text-xs font-medium text-accent hover:underline">{{ t('common.viewAll') }}</RouterLink>
        </div>
        <div class="grid grid-cols-2 gap-4 lg:grid-cols-4">
          <StatTile v-for="k in salesKpis" :key="k.label" :label="k.label" :value="k.value" :hint="k.hint" :hint-tone="k.tone" />
        </div>
      </section>

      <!-- Purchasing -->
      <section v-if="canSeePurchasing && purchasingKpis && purchasingKpis.length" class="space-y-3">
        <div class="flex items-center gap-2">
          <Truck :size="16" class="text-accent" />
          <h3 class="text-sm font-semibold text-text">{{ t('nav.purchasing') }}</h3>
          <RouterLink :to="{ name: 'purchasing' }" class="ml-auto text-xs font-medium text-accent hover:underline">{{ t('common.viewAll') }}</RouterLink>
        </div>
        <div class="grid grid-cols-2 gap-4 lg:grid-cols-4">
          <StatTile v-for="k in purchasingKpis" :key="k.label" :label="k.label" :value="k.value" :hint="k.hint" :hint-tone="k.tone" />
        </div>
      </section>

      <!-- Inventory -->
      <section v-if="canSeeInventory && inventoryKpis && inventoryKpis.length" class="space-y-3">
        <div class="flex items-center gap-2">
          <Boxes :size="16" class="text-accent" />
          <h3 class="text-sm font-semibold text-text">{{ t('nav.inventory') }}</h3>
          <RouterLink :to="{ name: 'inventory' }" class="ml-auto text-xs font-medium text-accent hover:underline">{{ t('common.viewAll') }}</RouterLink>
        </div>
        <div class="grid grid-cols-2 gap-4 lg:grid-cols-3">
          <StatTile v-for="k in inventoryKpis" :key="k.label" :label="k.label" :value="k.value" :hint="k.hint" :hint-tone="k.tone" />
        </div>
      </section>

      <!-- Quick links: primary navigation when the user has no operational section. -->
      <div v-if="!hasOperational" class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
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
