<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink, type RouteLocationRaw } from 'vue-router'
import { TrendingDown, TrendingUp } from 'lucide-vue-next'

const props = defineProps<{
  label: string
  value: string
  /** Optional sub-line (e.g. "1.234 overdue"). */
  hint?: string
  /** Tone of the hint chip. */
  hintTone?: 'neutral' | 'positive' | 'negative'
  delta?: number
  /** When set, the tile becomes a keyboard-focusable link to this route (drill-down). */
  to?: RouteLocationRaw
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

// Render as a RouterLink when `to` is given (keyboard-focusable, with hover/focus affordance);
// otherwise a plain non-interactive section.
const root = computed(() => (props.to ? RouterLink : 'section'))
</script>

<template>
  <component
    :is="root"
    :to="to"
    class="block rounded-card border border-border bg-surface shadow-card p-5"
    :class="to && 'transition-colors hover:border-accent focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent'"
  >
    <p class="text-sm text-text-muted">{{ label }}</p>
    <p class="mt-2 text-kpi text-text tnum">{{ value }}</p>
    <div class="mt-2 flex items-center gap-1.5 text-sm">
      <span v-if="delta !== undefined" class="inline-flex items-center gap-1" :class="delta >= 0 ? 'text-positive' : 'text-negative'">
        <component :is="delta >= 0 ? TrendingUp : TrendingDown" :size="16" />
        {{ Math.abs(delta) }}%
      </span>
      <span v-if="hint" :class="hintClass">{{ hint }}</span>
    </div>
  </component>
</template>
