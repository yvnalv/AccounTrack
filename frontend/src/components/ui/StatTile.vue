<script setup lang="ts">
import { computed } from 'vue'
import { TrendingDown, TrendingUp } from 'lucide-vue-next'

const props = defineProps<{
  label: string
  value: string
  /** Optional sub-line (e.g. "1.234 overdue"). */
  hint?: string
  /** Tone of the hint chip. */
  hintTone?: 'neutral' | 'positive' | 'negative'
  delta?: number
}>()

const hintClass = computed(() => {
  switch (props.hintTone) {
    case 'positive':
      return 'text-positive'
    case 'negative':
      return 'text-negative'
    default:
      return 'text-text-muted'
  }
})
</script>

<template>
  <section class="rounded-card border border-border bg-surface shadow-card p-5">
    <p class="text-sm text-text-muted">{{ label }}</p>
    <p class="mt-2 text-kpi text-text tnum">{{ value }}</p>
    <div class="mt-2 flex items-center gap-1.5 text-sm">
      <span v-if="delta !== undefined" class="inline-flex items-center gap-1" :class="delta >= 0 ? 'text-positive' : 'text-negative'">
        <component :is="delta >= 0 ? TrendingUp : TrendingDown" :size="16" />
        {{ Math.abs(delta) }}%
      </span>
      <span v-if="hint" :class="hintClass">{{ hint }}</span>
    </div>
  </section>
</template>
