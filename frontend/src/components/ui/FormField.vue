<script setup lang="ts">
defineProps<{ label: string; hint?: string; required?: boolean }>()
</script>

<template>
  <label class="block">
    <span class="mb-1.5 block text-sm font-medium text-text">
      {{ label }}<span v-if="required" class="text-negative"> *</span>
    </span>
    <slot />
    <span v-if="hint" class="mt-1 block text-xs text-text-muted">{{ hint }}</span>
  </label>
</template>
