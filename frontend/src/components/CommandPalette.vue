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
  Banknote,
  FileText,
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
  /** Target route name for a navigation command; used to gate it by the route's permission. */
  route?: string
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
  const n = (id: string, label: string, icon: Component, route: string): Command =>
    ({ id, label, group: nav, icon, run: go(route), route })
  return [
    n('dashboard', t('nav.dashboard'), LayoutDashboard, 'dashboard'),
    n('sales', t('sales.title'), ShoppingCart, 'sales'),
    n('sales-new', t('sales.new'), Plus, 'salesOrderCreate'),
    n('sales-invoices', t('invoiceList.salesTitle'), FileText, 'salesInvoices'),
    n('sales-payments', `${t('sales.title')} · ${t('paymentList.salesTitle')}`, Banknote, 'customerPayments'),
    n('sales-pay', t('sales.receivePayment'), Wallet, 'salesReceivePayment'),
    n('purchasing', t('purchasing.title'), Truck, 'purchasing'),
    n('po-new', t('purchasing.new'), Plus, 'purchaseOrderCreate'),
    n('po-invoices', t('invoiceList.purchaseTitle'), FileText, 'purchaseInvoices'),
    n('po-payments', `${t('purchasing.title')} · ${t('paymentList.purchaseTitle')}`, Banknote, 'supplierPayments'),
    n('po-pay', t('purchasing.paySupplier'), Wallet, 'purchasingPaySupplier'),
    n('inventory', t('inventory.title'), Boxes, 'inventory'),
    n('inventory-valuation', t('inventory.valuation.title'), Boxes, 'inventoryValuation'),
    n('acc-tb', t('accounting.tabs.trialBalance'), BookOpen, 'accountingTrialBalance'),
    n('acc-pl', t('accounting.tabs.profitLoss'), BookOpen, 'accountingProfitLoss'),
    n('acc-bs', t('accounting.tabs.balanceSheet'), BookOpen, 'accountingBalanceSheet'),
    n('acc-cf', t('accounting.tabs.cashFlow'), BookOpen, 'accountingCashFlow'),
    n('acc-gl', t('accounting.tabs.generalLedger'), BookOpen, 'accountingGeneralLedger'),
    n('acc-vat', t('accounting.tabs.vat'), BookOpen, 'accountingVat'),
    n('acc-periods', t('accounting.tabs.periods'), BookOpen, 'accountingPeriods'),
    n('md-products', t('masterData.tabs.products'), Database, 'masterDataProducts'),
    n('md-customers', t('masterData.tabs.customers'), Database, 'masterDataCustomers'),
    n('md-suppliers', t('masterData.tabs.suppliers'), Database, 'masterDataSuppliers'),
    n('md-warehouses', t('masterData.tabs.warehouses'), Database, 'masterDataWarehouses'),
    n('expenses', t('nav.expenses'), Wallet, 'expenses'),
    n('settings', t('nav.settings'), Settings, 'settings'),
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
    { id: 'signout', label: t('common.signOut'), group: actions, icon: LogOut, run: async () => { await auth.logout(); void router.push({ name: 'login' }) } },
  ]
})

// Hide navigation commands the user can't reach — same permission the router guard + sidebar enforce.
const visible = computed(() =>
  commands.value.filter((c) => {
    if (!c.route) return true
    const required = router.resolve({ name: c.route }).meta.permission
    return !required || auth.has(required)
  }),
)

const filtered = computed(() => {
  const q = query.value.trim().toLowerCase()
  return q ? visible.value.filter((c) => c.label.toLowerCase().includes(q) || c.group.toLowerCase().includes(q)) : visible.value
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
