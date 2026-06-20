<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch, type Component } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import {
  LayoutDashboard,
  ShoppingCart,
  Truck,
  Boxes,
  BookOpen,
  Database,
  Wallet,
  Plus,
  Search,
  Moon,
  Languages,
  LogOut,
  Settings,
  CornerDownLeft,
} from 'lucide-vue-next'
import { useCommandPalette } from '@/composables/useCommandPalette'
import { useThemeStore } from '@/stores/theme'
import { useAuthStore } from '@/stores/auth'
import { persistLocale, type Locale } from '@/i18n'

interface Command {
  id: string
  label: string
  group: string
  icon: Component
  run: () => void
}

const { isOpen, close, toggle } = useCommandPalette()
const router = useRouter()
const { t, locale } = useI18n()
const theme = useThemeStore()
const auth = useAuthStore()

const query = ref('')
const activeIndex = ref(0)
const inputEl = ref<HTMLInputElement | null>(null)

const commands = computed<Command[]>(() => {
  const nav = t('command.navigate')
  const actions = t('command.actions')
  const go = (name: string) => () => router.push({ name })
  return [
    { id: 'dashboard', label: t('nav.dashboard'), group: nav, icon: LayoutDashboard, run: go('dashboard') },
    { id: 'sales', label: t('sales.title'), group: nav, icon: ShoppingCart, run: go('sales') },
    { id: 'sales-new', label: t('sales.new'), group: nav, icon: Plus, run: go('salesOrderCreate') },
    { id: 'sales-pay', label: t('sales.receivePayment'), group: nav, icon: Wallet, run: go('salesReceivePayment') },
    { id: 'purchasing', label: t('purchasing.title'), group: nav, icon: Truck, run: go('purchasing') },
    { id: 'po-new', label: t('purchasing.new'), group: nav, icon: Plus, run: go('purchaseOrderCreate') },
    { id: 'po-pay', label: t('purchasing.paySupplier'), group: nav, icon: Wallet, run: go('purchasingPaySupplier') },
    { id: 'inventory', label: t('inventory.title'), group: nav, icon: Boxes, run: go('inventory') },
    { id: 'acc-tb', label: t('accounting.tabs.trialBalance'), group: nav, icon: BookOpen, run: go('accountingTrialBalance') },
    { id: 'acc-pl', label: t('accounting.tabs.profitLoss'), group: nav, icon: BookOpen, run: go('accountingProfitLoss') },
    { id: 'acc-bs', label: t('accounting.tabs.balanceSheet'), group: nav, icon: BookOpen, run: go('accountingBalanceSheet') },
    { id: 'acc-vat', label: t('accounting.tabs.vat'), group: nav, icon: BookOpen, run: go('accountingVat') },
    { id: 'md-products', label: t('masterData.tabs.products'), group: nav, icon: Database, run: go('masterDataProducts') },
    { id: 'md-customers', label: t('masterData.tabs.customers'), group: nav, icon: Database, run: go('masterDataCustomers') },
    { id: 'md-suppliers', label: t('masterData.tabs.suppliers'), group: nav, icon: Database, run: go('masterDataSuppliers') },
    { id: 'md-warehouses', label: t('masterData.tabs.warehouses'), group: nav, icon: Database, run: go('masterDataWarehouses') },
    { id: 'expenses', label: t('nav.expenses'), group: nav, icon: Wallet, run: go('expenses') },
    { id: 'settings', label: t('nav.settings'), group: nav, icon: Settings, run: go('settings') },
    { id: 'theme', label: t('command.toggleTheme'), group: actions, icon: Moon, run: () => theme.toggle() },
    {
      id: 'lang',
      label: t('command.toggleLanguage'),
      group: actions,
      icon: Languages,
      run: () => {
        const next: Locale = locale.value === 'id' ? 'en' : 'id'
        locale.value = next
        persistLocale(next)
      },
    },
    { id: 'signout', label: t('common.signOut'), group: actions, icon: LogOut, run: () => { auth.clear(); router.push({ name: 'login' }) } },
  ]
})

const filtered = computed(() => {
  const q = query.value.trim().toLowerCase()
  return q ? commands.value.filter((c) => c.label.toLowerCase().includes(q) || c.group.toLowerCase().includes(q)) : commands.value
})

// Show a group header before the first item of each group (over the filtered order).
function isGroupStart(i: number): boolean {
  return i === 0 || filtered.value[i].group !== filtered.value[i - 1].group
}

function runActive() {
  const cmd = filtered.value[activeIndex.value]
  if (cmd) {
    close()
    cmd.run()
  }
}

function move(delta: number) {
  const n = filtered.value.length
  if (n === 0) return
  activeIndex.value = (activeIndex.value + delta + n) % n
}

watch(query, () => (activeIndex.value = 0))
watch(isOpen, async (open) => {
  if (open) {
    query.value = ''
    activeIndex.value = 0
    await nextTick()
    inputEl.value?.focus()
  }
})

function onGlobalKey(e: KeyboardEvent) {
  if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') {
    e.preventDefault()
    toggle()
  }
}
onMounted(() => document.addEventListener('keydown', onGlobalKey))
onUnmounted(() => document.removeEventListener('keydown', onGlobalKey))
</script>

<template>
  <Teleport to="body">
    <Transition name="cmd">
      <div
        v-if="isOpen"
        class="fixed inset-0 z-[60] flex items-start justify-center bg-black/40 px-4 pt-[14vh]"
        @click.self="close"
      >
        <div class="w-full max-w-xl overflow-hidden rounded-card border border-border bg-surface shadow-card">
          <!-- Search -->
          <div class="flex items-center gap-2.5 border-b border-border px-4">
            <Search :size="18" class="text-text-muted" />
            <input
              ref="inputEl"
              v-model="query"
              :placeholder="t('command.placeholder')"
              class="h-12 flex-1 bg-transparent text-sm text-text outline-none placeholder:text-text-muted"
              @keydown.down.prevent="move(1)"
              @keydown.up.prevent="move(-1)"
              @keydown.enter.prevent="runActive"
              @keydown.esc.prevent="close"
            />
            <kbd class="rounded bg-surface-2 px-1.5 py-0.5 text-[11px] font-medium text-text-muted">ESC</kbd>
          </div>

          <!-- Results -->
          <div class="max-h-[50vh] overflow-y-auto p-2">
            <p v-if="filtered.length === 0" class="px-3 py-8 text-center text-sm text-text-muted">
              {{ t('command.empty') }}
            </p>
            <template v-for="(cmd, i) in filtered" :key="cmd.id">
              <p
                v-if="isGroupStart(i)"
                class="px-3 pb-1 pt-2 text-[11px] font-semibold uppercase tracking-wider text-text-muted"
              >
                {{ cmd.group }}
              </p>
              <button
                type="button"
                class="flex w-full items-center gap-3 rounded-control px-3 py-2 text-left text-sm transition-colors"
                :class="i === activeIndex ? 'bg-accent-soft text-text' : 'text-text hover:bg-surface-2'"
                @mousemove="activeIndex = i"
                @click="runActive"
              >
                <component :is="cmd.icon" :size="16" class="text-text-muted" />
                <span class="flex-1">{{ cmd.label }}</span>
                <CornerDownLeft v-if="i === activeIndex" :size="14" class="text-text-muted" />
              </button>
            </template>
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.cmd-enter-active,
.cmd-leave-active {
  transition: opacity 120ms ease;
}
.cmd-enter-from,
.cmd-leave-to {
  opacity: 0;
}
</style>
