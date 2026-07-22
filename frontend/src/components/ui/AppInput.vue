<script setup lang="ts">
const model = defineModel<string>()
withDefaults(defineProps<{ type?: 'text' | 'date' | 'email' | 'number'; placeholder?: string }>(), {
  type: 'text',
})
</script>

<template>
  <input v-model="model" :type="type" :placeholder="placeholder" class="field-input" />
</template>
