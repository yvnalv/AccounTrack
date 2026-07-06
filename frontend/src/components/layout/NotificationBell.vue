<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { Bell } from 'lucide-vue-next'
import { useNotificationStore } from '@/stores/notifications'
import { timeAgo } from '@/lib/format'

const { t, locale } = useI18n()
const store = useNotificationStore()

const open = ref(false)
const root = ref<HTMLElement | null>(null)

const badge = computed(() => (store.unreadCount > 9 ? '9+' : String(store.unreadCount)))

function toggle() {
  open.value = !open.value
}

function close() {
  open.value = false
}

function onClickOutside(e: MouseEvent) {
  if (open.value && root.value && !root.value.contains(e.target as Node)) {
    close()
  }
}

function onKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape') close()
}

onMounted(() => {
  store.start()
  document.addEventListener('mousedown', onClickOutside)
  document.addEventListener('keydown', onKeydown)
})

onBeforeUnmount(() => {
  store.stop()
  document.removeEventListener('mousedown', onClickOutside)
  document.removeEventListener('keydown', onKeydown)
})
</script>

<template>
  <div ref="root" class="relative">
    <button
      type="button"
      class="relative grid h-9 w-9 place-items-center rounded-full border border-border bg-surface text-text-muted transition-colors hover:text-text hover:bg-surface-2"
      :class="{ 'text-text bg-surface-2': open }"
      :aria-label="t('notifications.ariaLabel')"
      :aria-expanded="open"
      @click="toggle"
    >
      <Bell :size="18" />
      <span
        v-if="store.unreadCount > 0"
        class="absolute -right-0.5 -top-0.5 grid min-w-[1.05rem] place-items-center rounded-full bg-accent px-1 text-[0.625rem] font-semibold leading-4 text-accent-contrast"
      >
        {{ badge }}
      </span>
    </button>

    <Transition
      enter-active-class="transition duration-150 ease-out"
      enter-from-class="opacity-0 -translate-y-1"
      leave-active-class="transition duration-100 ease-in"
      leave-to-class="opacity-0 -translate-y-1"
    >
      <div
        v-if="open"
        class="absolute right-0 z-50 mt-2 w-80 overflow-hidden rounded-lg border border-border bg-surface shadow-lg sm:w-96"
      >
        <div class="flex items-center justify-between border-b border-border px-4 py-3">
          <h2 class="text-sm font-semibold text-text">{{ t('notifications.title') }}</h2>
          <button
            v-if="store.unreadCount > 0"
            type="button"
            class="text-xs font-medium text-accent transition-colors hover:underline"
            @click="store.markAllRead()"
          >
            {{ t('notifications.markAllRead') }}
          </button>
        </div>

        <div class="max-h-[24rem] overflow-y-auto">
          <p
            v-if="store.loaded && store.items.length === 0"
            class="px-4 py-10 text-center text-sm text-text-muted"
          >
            {{ t('notifications.empty') }}
          </p>
          <p
            v-else-if="!store.loaded && store.loading"
            class="px-4 py-10 text-center text-sm text-text-muted"
          >
            {{ t('common.loading') }}
          </p>

          <ul v-else>
            <li v-for="n in store.items" :key="n.id">
              <button
                type="button"
                class="flex w-full gap-3 border-b border-border px-4 py-3 text-left transition-colors last:border-0 hover:bg-surface-2"
                :class="{ 'bg-accent-soft': !n.isRead }"
                @click="store.markRead(n.id)"
              >
                <span
                  class="mt-1.5 h-2 w-2 shrink-0 rounded-full"
                  :class="n.isRead ? 'bg-transparent' : 'bg-accent'"
                  aria-hidden="true"
                />
                <span class="min-w-0 flex-1">
                  <span class="block truncate text-sm text-text" :class="{ 'font-semibold': !n.isRead }">
                    {{ n.title }}
                  </span>
                  <span class="mt-0.5 block line-clamp-2 text-xs text-text-muted">{{ n.body }}</span>
                  <span class="mt-1 block text-[0.6875rem] text-text-muted">
                    {{ timeAgo(n.createdAt, locale) }}
                  </span>
                </span>
              </button>
            </li>
          </ul>
        </div>
      </div>
    </Transition>
  </div>
</template>
