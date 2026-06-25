<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { RefreshCw, RotateCcw, CheckCircle2, AlertTriangle } from 'lucide-vue-next'
import { approvalApi } from '@/lib/approval'
import type { DeadLetterEvent } from '@/types/approval'
import AppButton from '@/components/ui/AppButton.vue'

const { t, locale } = useI18n()

const rows = ref<DeadLetterEvent[]>([])
const loading = ref(true)
const retrying = ref<string | null>(null)
const error = ref('')

function formatWhen(iso: string): string {
  const d = new Date(iso)
  return Number.isNaN(d.getTime())
    ? iso
    : d.toLocaleString(locale.value, { dateStyle: 'medium', timeStyle: 'short' })
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    rows.value = await approvalApi.deadLetters()
  } catch {
    error.value = t('settings.outbox.loadFailed')
  } finally {
    loading.value = false
  }
}
onMounted(load)

async function retry(row: DeadLetterEvent) {
  retrying.value = row.id
  error.value = ''
  try {
    await approvalApi.retryDeadLetter(row.id)
    // The dispatcher redelivers within a second or two; drop it from the list now and refresh shortly.
    rows.value = rows.value.filter((r) => r.id !== row.id)
    setTimeout(load, 2500)
  } catch {
    error.value = t('settings.outbox.retryFailed')
    await load()
  } finally {
    retrying.value = null
  }
}
</script>

<template>
  <div class="space-y-4">
    <div class="flex items-center justify-between gap-3">
      <p class="text-sm text-text-muted">{{ t('settings.outbox.subtitle') }}</p>
      <AppButton variant="ghost" :disabled="loading" @click="load">
        <RefreshCw :size="16" :class="{ 'animate-spin': loading }" /> {{ t('settings.outbox.refresh') }}
      </AppButton>
    </div>

    <p v-if="error" class="text-sm text-negative">{{ error }}</p>

    <div v-if="loading" class="text-sm text-text-muted">{{ t('common.loading') }}</div>

    <!-- Empty: the healthy state — nothing has failed -->
    <div
      v-else-if="!rows.length"
      class="flex items-center gap-3 rounded-lg border border-border bg-surface-2 px-4 py-6 text-sm text-text-muted"
    >
      <CheckCircle2 :size="18" class="text-positive" />
      {{ t('settings.outbox.empty') }}
    </div>

    <div v-else class="overflow-hidden rounded-lg border border-border">
      <table class="w-full text-sm">
        <thead class="bg-surface-2 text-left text-xs text-text-muted">
          <tr>
            <th class="px-3 py-2 font-medium">{{ t('settings.outbox.event') }}</th>
            <th class="px-3 py-2 font-medium">{{ t('settings.outbox.occurred') }}</th>
            <th class="px-3 py-2 font-medium">{{ t('settings.outbox.attempts') }}</th>
            <th class="px-3 py-2"></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="r in rows" :key="r.id" class="border-t border-border align-top">
            <td class="px-3 py-2">
              <span class="font-medium text-text">{{ r.eventType }}</span>
              <span
                v-if="r.error"
                class="mt-1 flex items-start gap-1.5 text-xs text-text-muted"
                :title="r.error"
              >
                <AlertTriangle :size="13" class="mt-0.5 shrink-0 text-warning" />
                <span class="line-clamp-2">{{ r.error }}</span>
              </span>
            </td>
            <td class="whitespace-nowrap px-3 py-2 text-text-muted">{{ formatWhen(r.occurredOnUtc) }}</td>
            <td class="px-3 py-2 text-text-muted">{{ r.attempts }}</td>
            <td class="px-3 py-2 text-right">
              <AppButton variant="ghost" :disabled="retrying === r.id" @click="retry(r)">
                <RotateCcw :size="15" :class="{ 'animate-spin': retrying === r.id }" />
                {{ retrying === r.id ? t('settings.outbox.retrying') : t('settings.outbox.retry') }}
              </AppButton>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
