import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { notificationApi } from '@/lib/notifications'
import type { NotificationDto } from '@/types/api'

// No websocket in the stack yet (roadmap infra), so the bell polls quietly while it's mounted.
const POLL_MS = 60_000

export const useNotificationStore = defineStore('notifications', () => {
  const items = ref<NotificationDto[]>([])
  const loading = ref(false)
  const loaded = ref(false)
  let timer: number | null = null

  const unreadCount = computed(() => items.value.filter((n) => !n.isRead).length)

  async function load() {
    loading.value = true
    try {
      items.value = await notificationApi.list()
      loaded.value = true
    } catch {
      // Keep the last-known list; a transient failure shouldn't clear the bell or surface an error.
    } finally {
      loading.value = false
    }
  }

  async function markRead(id: string) {
    const n = items.value.find((x) => x.id === id)
    if (!n || n.isRead) return
    // Optimistic: flip locally, revert if the server rejects it.
    n.isRead = true
    n.readAtUtc = new Date().toISOString()
    try {
      await notificationApi.markRead(id)
    } catch {
      n.isRead = false
      n.readAtUtc = null
    }
  }

  async function markAllRead() {
    const unread = items.value.filter((n) => !n.isRead)
    await Promise.all(unread.map((n) => markRead(n.id)))
  }

  /** Begin polling (called when the bell mounts); loads immediately, then on an interval. */
  function start() {
    void load()
    if (timer === null) {
      timer = window.setInterval(() => void load(), POLL_MS)
    }
  }

  function stop() {
    if (timer !== null) {
      window.clearInterval(timer)
      timer = null
    }
  }

  return { items, loading, loaded, unreadCount, load, markRead, markAllRead, start, stop }
})
