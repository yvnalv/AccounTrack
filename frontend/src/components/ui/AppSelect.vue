<script setup lang="ts">
import type { SelectOption } from './types'

const model = defineModel<string>()
defineProps<{ options: SelectOption[]; placeholder?: string }>()
</script>

<template>
  <select v-model="model" class="field-input appearance-none pr-8">
    <option v-if="placeholder" value="" disabled>{{ placeholder }}</option>
    <option v-for="opt in options" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
  </select>
</template>
