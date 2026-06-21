<script setup lang="ts">
export interface Insight {
  label: string
  value: string
  tone?: 'accent' | 'positive' | 'negative' | 'neutral'
}

defineProps<{ items: Insight[] }>()

function toneClass(tone?: Insight['tone']): string {
  switch (tone) {
    case 'positive':
      return 'text-positive'
    case 'negative':
      return 'text-negative'
    case 'accent':
      return 'text-accent'
    default:
      return 'text-text'
  }
}
</script>

<template>
  <div class="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
    <div
      v-for="it in items"
      :key="it.label"
      class="rounded-card border border-border bg-surface px-4 py-3 shadow-card"
    >
      <p class="text-xs text-text-muted">{{ it.label }}</p>
      <p class="mt-1 text-lg font-semibold tnum" :class="toneClass(it.tone)">{{ it.value }}</p>
    </div>
  </div>
</template>
