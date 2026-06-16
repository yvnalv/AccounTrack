<script setup lang="ts">
import type { Component } from 'vue'
import { RouterLink, type RouteLocationRaw } from 'vue-router'
import { useI18n } from 'vue-i18n'
import {
  LayoutDashboard,
  ShoppingCart,
  Truck,
  Boxes,
  BookOpen,
  Database,
  CheckSquare,
  Settings,
  Search,
} from 'lucide-vue-next'

const { t } = useI18n()

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
      { to: { name: 'accounting' }, icon: BookOpen, label: t('nav.accounting') },
    ],
  },
  {
    label: t('nav.sections.system'),
    items: [
      { to: { name: 'masterData' }, icon: Database, label: t('nav.masterData') },
      { to: { name: 'approvals' }, icon: CheckSquare, label: t('nav.approvals') },
      { to: { name: 'settings' }, icon: Settings, label: t('nav.settings') },
    ],
  },
]
</script>

<template>
  <aside class="flex h-full w-[248px] shrink-0 flex-col bg-sidebar text-sidebar-text">
    <!-- Brand -->
    <div class="flex items-center gap-2.5 px-5 py-5">
      <svg width="32" height="32" viewBox="0 0 40 40" fill="none" aria-hidden="true">
        <rect width="40" height="40" rx="11" fill="#007E6E" />
        <g fill="#FFFFFF">
          <rect x="11" y="21" width="5" height="8" rx="2" />
          <rect x="17.5" y="16" width="5" height="13" rx="2" />
          <rect x="24" y="11" width="5" height="18" rx="2" />
        </g>
      </svg>
      <span class="text-[17px] font-bold tracking-tight text-white">
        Accoun<span class="text-accent">track</span>
      </span>
    </div>

    <!-- Search (⌘K placeholder) -->
    <div class="px-4 pb-2">
      <button
        type="button"
        class="flex w-full items-center gap-2 rounded-control bg-white/5 px-3 h-10 text-sm text-sidebar-muted transition-colors hover:bg-white/10"
      >
        <Search :size="16" />
        <span class="flex-1 text-left">{{ t('common.search') }}</span>
        <kbd class="rounded bg-white/10 px-1.5 py-0.5 text-[11px] font-medium">⌘K</kbd>
      </button>
    </div>

    <!-- Nav -->
    <nav class="flex-1 overflow-y-auto px-3 py-2">
      <div v-for="group in groups" :key="group.label" class="mb-4">
        <p class="px-3 pb-1.5 text-[11px] font-semibold uppercase tracking-wider text-sidebar-muted">
          {{ group.label }}
        </p>
        <RouterLink
          v-for="item in group.items"
          :key="item.label"
          :to="item.to"
          class="group mb-0.5 flex items-center gap-3 rounded-control px-3 h-10 text-sm font-medium text-sidebar-text transition-colors hover:bg-white/5"
          :active-class="item.exact ? '' : '!bg-accent !text-accent-contrast'"
          exact-active-class="!bg-accent !text-accent-contrast"
        >
          <component :is="item.icon" :size="18" />
          {{ item.label }}
        </RouterLink>
      </div>
    </nav>
  </aside>
</template>
