<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(
  defineProps<{
    variant?: 'primary' | 'secondary' | 'ghost' | 'danger'
    type?: 'button' | 'submit'
    disabled?: boolean
    block?: boolean
  }>(),
  { variant: 'primary', type: 'button', disabled: false, block: false },
)

const classes = computed(() => {
  const base =
    'inline-flex items-center justify-center gap-2 rounded-control px-4 h-10 text-sm font-medium transition-colors disabled:opacity-50 disabled:cursor-not-allowed'
  const variants: Record<string, string> = {
    primary: 'bg-accent text-accent-contrast hover:bg-accent-hover active:bg-accent-active',
    secondary: 'bg-surface text-text border border-border hover:bg-surface-2',
    ghost: 'text-text hover:bg-surface-2',
    danger: 'bg-negative text-white hover:opacity-90',
  }
  return [base, variants[props.variant], props.block ? 'w-full' : ''].join(' ')
})
</script>

<template>
  <button :type="type" :disabled="disabled" :class="classes">
    <slot />
  </button>
</template>
