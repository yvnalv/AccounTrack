<script setup lang="ts">
import { onMounted, onUnmounted } from 'vue'
import { X } from 'lucide-vue-next'

const open = defineModel<boolean>({ required: true })
defineProps<{ title: string }>()

function onKey(e: KeyboardEvent) {
  if (e.key === 'Escape') open.value = false
}
onMounted(() => document.addEventListener('keydown', onKey))
onUnmounted(() => document.removeEventListener('keydown', onKey))
</script>

<template>
  <Teleport to="body">
    <Transition name="modal">
      <div
        v-if="open"
        class="fixed inset-0 z-50 grid place-items-center bg-black/40 p-4"
        @click.self="open = false"
      >
        <div class="w-full max-w-lg rounded-card border border-border bg-surface shadow-card">
          <header class="flex items-center justify-between border-b border-border px-5 py-3.5">
            <h2 class="text-sm font-semibold text-text">{{ title }}</h2>
            <button
              type="button"
              class="grid h-8 w-8 place-items-center rounded-control text-text-muted hover:bg-surface-2 hover:text-text"
              @click="open = false"
            >
              <X :size="18" />
            </button>
          </header>
          <div class="p-5">
            <slot />
          </div>
          <footer v-if="$slots.footer" class="flex items-center justify-end gap-3 border-t border-border px-5 py-3.5">
            <slot name="footer" />
          </footer>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.modal-enter-active,
.modal-leave-active {
  transition: opacity 150ms ease;
}
.modal-enter-from,
.modal-leave-to {
  opacity: 0;
}
</style>
