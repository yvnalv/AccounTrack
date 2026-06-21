import { defineStore } from 'pinia'
import { ref } from 'vue'

const STORAGE_KEY = 'accountrack.sidebar.collapsed'

export const useLayoutStore = defineStore('layout', () => {
  /** Desktop icon-only rail (persisted). */
  const collapsed = ref(localStorage.getItem(STORAGE_KEY) === '1')
  /** Mobile off-canvas drawer open state (not persisted). */
  const mobileOpen = ref(false)

  function toggleCollapsed() {
    collapsed.value = !collapsed.value
    localStorage.setItem(STORAGE_KEY, collapsed.value ? '1' : '0')
  }

  const openMobile = () => (mobileOpen.value = true)
  const closeMobile = () => (mobileOpen.value = false)
  const toggleMobile = () => (mobileOpen.value = !mobileOpen.value)

  return { collapsed, mobileOpen, toggleCollapsed, openMobile, closeMobile, toggleMobile }
})
