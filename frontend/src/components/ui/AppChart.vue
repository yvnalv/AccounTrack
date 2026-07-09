<script setup lang="ts">
/*
 * Thin wrapper that owns the ECharts registration and the vue-echarts component. It is loaded as a
 * separate async chunk (see DashboardView) so the ~500 KB ECharts bundle is code-split out of the
 * route chunk and fetched in parallel — the dashboard's KPI cards paint without waiting for it.
 * Only the chart types actually used are registered (tree-shaken).
 */
import type { EChartsOption } from 'echarts'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { BarChart, LineChart, PieChart } from 'echarts/charts'
import { GridComponent, TooltipComponent, LegendComponent } from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'

use([BarChart, LineChart, PieChart, GridComponent, TooltipComponent, LegendComponent, CanvasRenderer])

defineProps<{ option: EChartsOption }>()
</script>

<template>
  <VChart :option="option" autoresize class="h-full w-full" />
</template>
