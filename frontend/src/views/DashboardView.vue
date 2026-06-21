<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { EChartsOption } from 'echarts'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { BarChart, LineChart, PieChart } from 'echarts/charts'
import { GridComponent, TooltipComponent, LegendComponent } from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'
import { http, unwrap } from '@/lib/api'
import { formatMoney, formatMoneyShort } from '@/lib/format'
import type { DashboardAging, DashboardSummary } from '@/types/api'
import { useThemeStore } from '@/stores/theme'
import AppCard from '@/components/ui/AppCard.vue'
import StatTile from '@/components/ui/StatTile.vue'

use([BarChart, LineChart, PieChart, GridComponent, TooltipComponent, LegendComponent, CanvasRenderer])

const { t } = useI18n()
const theme = useThemeStore()

const summary = ref<DashboardSummary | null>(null)
const loading = ref(true)
const error = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try {
    summary.value = await unwrap<DashboardSummary>(http.get('/dashboard/summary'))
  } catch {
    error.value = 'Could not load the dashboard.'
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
          <VChart :option="trendOption" autoresize class="h-[300px] w-full" />
        </AppCard>
        <AppCard :title="t('dashboard.expenseBreakdown')">
          <VChart v-if="hasExpense" :option="expenseOption" autoresize class="h-[300px] w-full" />
          <p v-else class="grid h-[300px] place-items-center text-sm text-text-muted">{{ t('dashboard.noData') }}</p>
        </AppCard>
      </div>

      <!-- Aging -->
      <div class="grid grid-cols-1 gap-5 lg:grid-cols-2">
        <AppCard :title="t('dashboard.receivableAging')">
          <VChart :option="arAgingOption" autoresize class="h-[260px] w-full" />
        </AppCard>
        <AppCard :title="t('dashboard.payableAging')">
          <VChart :option="apAgingOption" autoresize class="h-[260px] w-full" />
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
