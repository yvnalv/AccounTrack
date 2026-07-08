<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { ArrowLeft, ExternalLink } from 'lucide-vue-next'
import { salesApi } from '@/lib/sales'
import { accountingApi } from '@/lib/accounting'
import { formatMoney } from '@/lib/format'
import type { CustomerPayment } from '@/types/sales'
import AppCard from '@/components/ui/AppCard.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()

const doc = ref<CustomerPayment | null>(null)
const cashAccountLabel = ref('—')
const openItemNames = ref(new Map<string, string>())
const loading = ref(true)

const id = computed(() => String(route.params.id))
const currency = computed(() => doc.value?.currency ?? 'IDR')

async function load() {
  loading.value = true
  try {
    const payment = await salesApi.getCustomerPayment(id.value)
    doc.value = payment
    // Resolve the cash/bank account label and (best-effort) allocated invoice numbers.
    const [accounts, openItems] = await Promise.all([
      accountingApi.accounts().catch(() => []),
      accountingApi.arOpenItems(payment.customerId).catch(() => []),
    ])
    const acc = accounts.find((a) => a.id === payment.cashAccountId)
    if (acc) cashAccountLabel.value = `${acc.code} — ${acc.name}`
    openItemNames.value = new Map(openItems.map((o) => [o.id, o.documentNo]))
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="space-y-5">
    <button
      class="inline-flex items-center gap-1.5 text-sm text-text-muted hover:text-text"
      @click="router.back()"
    >
      <ArrowLeft :size="16" /> {{ t('common.back') }}
    </button>

    <p v-if="loading" class="text-sm text-text-muted">{{ t('docDetail.loading') }}</p>
    <p v-else-if="!doc" class="text-sm text-text-muted">{{ t('docDetail.notFound') }}</p>

    <template v-else>
      <div class="flex flex-wrap items-start justify-between gap-4">
        <div>
          <div class="flex items-center gap-3">
            <h2 class="text-xl font-semibold text-text">{{ doc.number }}</h2>
            <StatusBadge tone="info" :label="t('docDetail.paymentReceivedTitle')" />
            <StatusBadge
              :tone="doc.journalEntryId ? 'positive' : 'neutral'"
              :label="doc.journalEntryId ? t('docDetail.posted') : t('docDetail.notPosted')"
            />
          </div>
          <p class="mt-1 text-sm text-text-muted">{{ doc.paymentDate }}</p>
        </div>
        <button
          class="inline-flex items-center gap-1.5 rounded-button border border-border px-3 py-2 text-sm font-medium text-text-muted transition-colors hover:bg-surface-2 hover:text-text"
          @click="router.push({ name: 'masterDataCustomerDetail', params: { id: doc.customerId } })"
        >
          <ExternalLink :size="16" /> {{ t('docDetail.viewCustomer') }}
        </button>
      </div>

      <AppCard :title="t('docDetail.paymentReceivedTitle')">
        <dl class="grid grid-cols-2 gap-4 text-sm sm:grid-cols-4">
          <div>
            <dt class="text-xs text-text-muted">{{ t('docDetail.cashAccount') }}</dt>
            <dd class="text-text">{{ cashAccountLabel }}</dd>
          </div>
          <div>
            <dt class="text-xs text-text-muted">{{ t('docDetail.paymentDate') }}</dt>
            <dd class="text-text">{{ doc.paymentDate }}</dd>
          </div>
          <div>
            <dt class="text-xs text-text-muted">{{ t('docDetail.reference') }}</dt>
            <dd class="text-text">{{ doc.reference || '—' }}</dd>
          </div>
          <div>
            <dt class="text-xs text-text-muted">{{ t('docDetail.totalAmount') }}</dt>
            <dd class="text-text tnum">{{ formatMoney(doc.totalAmount, currency) }}</dd>
          </div>
        </dl>
      </AppCard>

      <AppCard :title="t('docDetail.allocations')" :padded="false">
        <table v-if="doc.allocations.length" class="w-full text-sm">
          <thead>
            <tr class="border-b border-border text-xs uppercase tracking-wide text-text-muted">
              <th class="px-4 py-2.5 text-left font-semibold">{{ t('docDetail.allocDocument') }}</th>
              <th class="px-4 py-2.5 text-right font-semibold">{{ t('docDetail.allocAmount') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(a, i) in doc.allocations" :key="i" class="border-b border-border last:border-0">
              <td class="px-4 py-2.5 text-text">{{ openItemNames.get(a.arOpenItemId) ?? '—' }}</td>
              <td class="px-4 py-2.5 text-right text-text tnum">{{ formatMoney(a.amount, currency) }}</td>
            </tr>
          </tbody>
        </table>
        <p v-else class="px-4 py-6 text-center text-sm text-text-muted">{{ t('docDetail.noAllocations') }}</p>
      </AppCard>

      <AppCard v-if="doc.notes" :title="t('docDetail.notes')">
        <p class="text-sm text-text">{{ doc.notes }}</p>
      </AppCard>
    </template>
  </div>
</template>
