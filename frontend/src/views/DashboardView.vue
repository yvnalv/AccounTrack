<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { EChartsOption } from 'echarts'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { BarChart } from 'echarts/charts'
import { GridComponent, TooltipComponent } from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'
import { http, unwrap } from '@/lib/api'
import { formatMoney } from '@/lib/format'
import type { DashboardSummary } from '@/types/api'
import { useThemeStore } from '@/stores/theme'
import AppCard from '@/components/ui/AppCard.vue'
import StatTile from '@/components/ui/StatTile.vue'

use([BarChart, GridComponent, TooltipComponent, CanvasRenderer])

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

function cssVar(name: string): string {
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim()
}

// Rebuild chart colors when the summary or theme changes.
const chartOption = computed<EChartsOption>(() => {
  // Reference theme.theme so the computed re-runs on toggle.
  void theme.theme
  const s = summary.value
  const accent = cssVar('--accent') || '#007E6E'
  const muted = cssVar('--text-muted') || '#6b7280'
  const border = cssVar('--border') || '#e7e9ec'
  const text = cssVar('--text') || '#15171a'

  return {
    grid: { top: 16, right: 12, bottom: 28, left: 48 },
    tooltip: {
      trigger: 'axis',
      backgroundColor: '#15171A',
      borderWidth: 0,
      textStyle: { color: '#fff', fontSize: 12 },
      axisPointer: { type: 'shadow', shadowStyle: { color: 'rgba(0,0,0,0.04)' } },
    },
    xAxis: {
      type: 'category',
      data: [t('dashboard.revenue'), t('dashboard.expense')],
      axisLine: { lineStyle: { color: border } },
      axisTick: { show: false },
      axisLabel: { color: muted },
    },
    yAxis: {
      type: 'value',
      splitLine: { lineStyle: { color: border, type: 'dashed' } },
      axisLabel: { color: muted },
    },
    series: [
      {
        type: 'bar',
        barWidth: 64,
        itemStyle: { borderRadius: [6, 6, 0, 0] },
        data: [
          { value: s?.revenueThisMonth ?? 0, itemStyle: { color: accent } },
          { value: s?.expenseThisMonth ?? 0, itemStyle: { color: muted } },
        ],
        label: { show: false },
      },
    ],
    textStyle: { color: text, fontFamily: 'Plus Jakarta Sans' },
  }
})

const currency = computed(() => summary.value?.currency ?? 'IDR')
const overdueAR = computed(() =>
  summary.value && summary.value.overdueReceivable > 0
    ? t('dashboard.overdue', { amount: formatMoney(summary.value.overdueReceivable, currency.value) })
    : undefined,
)
const overdueAP = computed(() =>
  summary.value && summary.value.overduePayable > 0
    ? t('dashboard.overdue', { amount: formatMoney(summary.value.overduePayable, currency.value) })
    : undefined,
)
</script>

<template>
  <div class="space-y-6">
    <p v-if="loading" class="text-sm text-text-muted">{{ t('common.loading') }}</p>

    <div v-else-if="error" class="text-sm text-negative">
      {{ error }}
      <button class="ml-2 underline" @click="load">{{ t('common.retry') }}</button>
    </div>

    <template v-else-if="summary">
      <!-- KPI row -->
      <div class="grid grid-cols-1 gap-5 sm:grid-cols-2 xl:grid-cols-4">
        <StatTile
          :label="t('dashboard.cashAndBank')"
          :value="formatMoney(summary.cashAndBank, currency)"
        />
        <StatTile
          :label="t('dashboard.accountsReceivable')"
          :value="formatMoney(summary.accountsReceivable, currency)"
          :hint="overdueAR"
          hint-tone="negative"
        />
        <StatTile
          :label="t('dashboard.accountsPayable')"
          :value="formatMoney(summary.accountsPayable, currency)"
          :hint="overdueAP"
          hint-tone="negative"
        />
        <StatTile
          :label="t('dashboard.netProfitMonth')"
          :value="formatMoney(summary.netProfitThisMonth, currency)"
          :hint-tone="summary.netProfitThisMonth >= 0 ? 'positive' : 'negative'"
        />
      </div>

      <!-- Revenue vs expense -->
      <AppCard :title="t('dashboard.revenueVsExpense')">
        <template #actions>
          <span class="text-xs text-text-muted">
            {{ t('dashboard.asOf', { date: summary.asOfDate }) }}
          </span>
        </template>
        <VChart :option="chartOption" autoresize class="h-[320px] w-full" />
      </AppCard>
    </template>
  </div>
</template>
