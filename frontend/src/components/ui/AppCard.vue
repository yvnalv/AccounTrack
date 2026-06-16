<script setup lang="ts">
withDefaults(defineProps<{ title?: string; padded?: boolean }>(), { padded: true })
</script>

<template>
  <section class="rounded-card border border-border bg-surface shadow-card">
    <header
      v-if="title || $slots.actions"
      class="flex items-center justify-between px-5 pt-4"
    >
      <h2 class="text-sm font-semibold text-text">{{ title }}</h2>
      <slot name="actions" />
    </header>
    <div :class="padded ? 'p-5' : ''">
      <slot />
    </div>
  </section>
</template>
