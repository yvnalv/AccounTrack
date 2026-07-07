<script setup lang="ts">
import type { Component } from 'vue'
import { watch } from 'vue'
import { RouterLink, useRoute, type RouteLocationRaw } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useCommandPalette } from '@/composables/useCommandPalette'
import { useLayoutStore } from '@/stores/layout'
import {
  LayoutDashboard,
  ShoppingCart,
  Truck,
  Boxes,
  BookOpen,
  CheckSquare,
  Settings,
  Receipt,
  Search,
  Package,
  Users,
  Building2,
  Warehouse,
  SlidersHorizontal,
  PanelLeftClose,
  PanelLeftOpen,
} from 'lucide-vue-next'

const { t } = useI18n()
const palette = useCommandPalette()
const layout = useLayoutStore()
const route = useRoute()

// Close the mobile drawer whenever the route changes.
watch(() => route.fullPath, () => layout.closeMobile())

interface NavItem {
  to: RouteLocationRaw
  icon: Component
  label: string
  exact?: boolean
}

const groups: { label: string; items: NavItem[] }[] = [
  {
    label: t('nav.sections.main'),
    items: [{ to: { name: 'dashboard' }, icon: LayoutDashboard, label: t('nav.dashboard'), exact: true }],
  },
  {
    label: t('nav.sections.operations'),
    items: [
      { to: { name: 'sales' }, icon: ShoppingCart, label: t('nav.sales') },
      { to: { name: 'purchasing' }, icon: Truck, label: t('nav.purchasing') },
      { to: { name: 'inventory' }, icon: Boxes, label: t('nav.inventory') },
      { to: { name: 'expenses' }, icon: Receipt, label: t('nav.expenses') },
      { to: { name: 'accounting' }, icon: BookOpen, label: t('nav.accounting') },
    ],
  },
  {
    label: t('nav.sections.masterData'),
    items: [
      { to: { name: 'masterDataProducts' }, icon: Package, label: t('nav.products') },
      { to: { name: 'masterDataCustomers' }, icon: Users, label: t('nav.customers') },
      { to: { name: 'masterDataSuppliers' }, icon: Building2, label: t('nav.suppliers') },
      { to: { name: 'masterDataWarehouses' }, icon: Warehouse, label: t('nav.warehouses') },
      { to: { name: 'masterData' }, icon: SlidersHorizontal, label: t('nav.setup') },
    ],
  },
  {
    label: t('nav.sections.system'),
    items: [
      { to: { name: 'approvals' }, icon: CheckSquare, label: t('nav.approvals') },
      { to: { name: 'settings' }, icon: Settings, label: t('nav.settings') },
    ],
  },
]
</script>

<template>
  <aside
    class="fixed inset-y-0 left-0 z-50 flex h-full w-[248px] shrink-0 flex-col bg-sidebar text-sidebar-text transition-all duration-200 ease-out lg:static lg:z-auto lg:translate-x-0"
    :class="[
      layout.mobileOpen ? 'translate-x-0' : '-translate-x-full',
      layout.collapsed ? 'lg:w-[72px]' : 'lg:w-[248px]',
    ]"
  >
    <!-- Brand + collapse toggle -->
    <div class="flex items-center gap-2.5 px-5 py-5" :class="layout.collapsed ? 'lg:px-0 lg:justify-center' : ''">
      <svg width="32" height="32" viewBox="0 0 40 40" fill="none" aria-hidden="true" class="shrink-0">
        <rect width="40" height="40" rx="11" fill="#007E6E" />
        <g fill="#FFFFFF">
          <rect x="11" y="21" width="5" height="8" rx="2" />
          <rect x="17.5" y="16" width="5" height="13" rx="2" />
          <rect x="24" y="11" width="5" height="18" rx="2" />
        </g>
      </svg>
      <span class="text-[17px] font-bold tracking-tight text-white" :class="layout.collapsed ? 'lg:hidden' : ''">
        Accoun<span class="text-accent">track</span>
      </span>
      <button
        type="button"
        class="ml-auto hidden h-8 w-8 place-items-center rounded-control text-sidebar-muted transition-colors hover:bg-white/10 hover:text-white lg:grid"
        :class="layout.collapsed ? 'lg:hidden' : ''"
        :aria-label="t('nav.collapse')"
        @click="layout.toggleCollapsed()"
      >
        <PanelLeftClose :size="18" />
      </button>
    </div>

    <!-- Search (⌘K) -->
    <div class="px-4 pb-2" :class="layout.collapsed ? 'lg:px-3' : ''">
      <button
        type="button"
        class="flex w-full items-center gap-2 rounded-control bg-white/5 px-3 h-10 text-sm text-sidebar-muted transition-colors hover:bg-white/10"
        :class="layout.collapsed ? 'lg:justify-center lg:px-0' : ''"
        :title="t('common.search')"
        @click="palette.open()"
      >
        <Search :size="16" class="shrink-0" />
        <span class="flex-1 text-left" :class="layout.collapsed ? 'lg:hidden' : ''">{{ t('common.search') }}</span>
        <kbd class="rounded bg-white/10 px-1.5 py-0.5 text-[11px] font-medium" :class="layout.collapsed ? 'lg:hidden' : ''">⌘K</kbd>
      </button>
    </div>

    <!-- Nav -->
    <nav class="flex-1 overflow-y-auto px-3 py-2">
      <div v-for="group in groups" :key="group.label" class="mb-4">
        <p
          class="px-3 pb-1.5 text-[11px] font-semibold uppercase tracking-wider text-sidebar-muted"
          :class="layout.collapsed ? 'lg:hidden' : ''"
        >
          {{ group.label }}
        </p>
        <RouterLink
          v-for="item in group.items"
          :key="item.label"
          :to="item.to"
          :title="item.label"
          class="group mb-0.5 flex items-center gap-3 rounded-control px-3 h-10 text-sm font-medium text-sidebar-text transition-colors hover:bg-white/5"
          :class="layout.collapsed ? 'lg:justify-center lg:px-0' : ''"
          :active-class="item.exact ? '' : '!bg-accent !text-accent-contrast'"
          exact-active-class="!bg-accent !text-accent-contrast"
        >
          <component :is="item.icon" :size="18" class="shrink-0" />
          <span :class="layout.collapsed ? 'lg:hidden' : ''">{{ item.label }}</span>
        </RouterLink>
      </div>
    </nav>

    <!-- Expand toggle (only shown when collapsed, desktop) -->
    <div v-if="layout.collapsed" class="hidden px-3 py-3 lg:block">
      <button
        type="button"
        class="grid h-10 w-full place-items-center rounded-control text-sidebar-muted transition-colors hover:bg-white/5 hover:text-white"
        :aria-label="t('nav.expand')"
        @click="layout.toggleCollapsed()"
      >
        <PanelLeftOpen :size="18" />
      </button>
    </div>
  </aside>
</template>
