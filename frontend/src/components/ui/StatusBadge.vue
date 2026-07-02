<script setup lang="ts">
import { computed } from 'vue'

type Tone = 'neutral' | 'positive' | 'negative' | 'warning' | 'info'

const props = defineProps<{ label: string; tone?: Tone; status?: string }>()

// Default tone mapping for document statuses (sales/purchasing share these names).
const statusTone: Record<string, Tone> = {
  Draft: 'neutral',
  PendingApproval: 'warning',
  Approved: 'info',
  Rejected: 'negative',
  Cancelled: 'neutral',
  Reversed: 'neutral',
  PartiallyDelivered: 'warning',
  Delivered: 'positive',
  PartiallyReceived: 'warning',
  Received: 'positive',
  Posted: 'positive',
  Open: 'info',
  PartiallyPaid: 'warning',
  Settled: 'positive',
}

const tone = computed<Tone>(
  () => props.tone ?? (props.status ? (statusTone[props.status] ?? 'neutral') : 'neutral'),
)

// Soft tinted badge via color-mix (reliable with CSS-variable colors).
const style = computed(() => {
  if (tone.value === 'neutral') {
    return { color: 'var(--text-muted)', backgroundColor: 'var(--surface-2)' }
  }
  const v = `var(--${tone.value})`
  return { color: v, backgroundColor: `color-mix(in srgb, ${v} 12%, transparent)` }
})
</script>

<template>
  <span
    class="inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium"
    :style="style"
  >
    {{ label }}
  </span>
</template>
