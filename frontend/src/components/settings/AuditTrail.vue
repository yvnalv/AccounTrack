<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { RefreshCw, ChevronRight, FileSearch } from 'lucide-vue-next'
import { auditApi } from '@/lib/audit'
import type { AuditAction, AuditEntryDto, Paged } from '@/types/audit'
import AppButton from '@/components/ui/AppButton.vue'
import AppInput from '@/components/ui/AppInput.vue'
import FormField from '@/components/ui/FormField.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'

const { t, locale } = useI18n()

const PAGE_SIZE = 50

const rows = ref<AuditEntryDto[]>([])
const page = ref(1)
const totalItems = ref(0)
const loading = ref(true)
const error = ref('')
const expanded = ref<string | null>(null)

// Filters (applied on submit, not on keystroke).
const entityType = ref('')
const fromDate = ref('')
const toDate = ref('')

const totalPages = computed(() => Math.max(1, Math.ceil(totalItems.value / PAGE_SIZE)))
const rangeFrom = computed(() => (totalItems.value === 0 ? 0 : (page.value - 1) * PAGE_SIZE + 1))
const rangeTo = computed(() => Math.min(page.value * PAGE_SIZE, totalItems.value))

const actionTone: Record<AuditAction, 'positive' | 'info' | 'negative'> = {
  Insert: 'positive',
  Update: 'info',
  Delete: 'negative',
}

function formatWhen(iso: string): string {
  const d = new Date(iso)
  return Number.isNaN(d.getTime())
    ? iso
    : d.toLocaleString(locale.value, { dateStyle: 'medium', timeStyle: 'short' })
}

/** Short, glanceable id ("a1b2c3d4"). Full value is exposed via title on hover. */
function shortId(id: string): string {
  return id.length > 8 ? id.slice(0, 8) : id
}

interface ChangeRow {
  field: string
  before: string
  after: string
  isUpdate: boolean
}

function render(value: unknown): string {
  if (value === null || value === undefined) return '—'
  if (typeof value === 'object') return JSON.stringify(value)
  return String(value)
}

/** Parses `changesJson` into displayable field rows (before → after for updates, value otherwise). */
function parseChanges(entry: AuditEntryDto): ChangeRow[] {
  let doc: Record<string, unknown>
  try {
    doc = JSON.parse(entry.changesJson) as Record<string, unknown>
  } catch {
    return []
  }
  return Object.entries(doc).map(([field, raw]) => {
    if (
      raw !== null &&
      typeof raw === 'object' &&
      ('old' in (raw as object) || 'new' in (raw as object))
    ) {
      const change = raw as { old?: unknown; new?: unknown }
      return { field, before: render(change.old), after: render(change.new), isUpdate: true }
    }
    return { field, before: '', after: render(raw), isUpdate: false }
  })
}

function toIsoStart(date: string): string | null {
  return date ? new Date(`${date}T00:00:00`).toISOString() : null
}
function toIsoEnd(date: string): string | null {
  return date ? new Date(`${date}T23:59:59.999`).toISOString() : null
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    const result: Paged<AuditEntryDto> = await auditApi.list({
      entityType: entityType.value.trim() || null,
      fromUtc: toIsoStart(fromDate.value),
      toUtc: toIsoEnd(toDate.value),
      page: page.value,
      pageSize: PAGE_SIZE,
    })
    rows.value = result.items
    totalItems.value = result.totalItems
    expanded.value = null
  } catch {
    error.value = t('settings.audit.loadFailed')
  } finally {
    loading.value = false
  }
}
onMounted(load)

function applyFilters() {
  page.value = 1
  load()
}

function goPage(next: number) {
  if (next < 1 || next > totalPages.value || next === page.value) return
  page.value = next
  load()
}

function toggle(id: string) {
  expanded.value = expanded.value === id ? null : id
}
</script>

<template>
  <div class="space-y-4">
    <p class="text-sm text-text-muted">{{ t('settings.audit.subtitle') }}</p>

    <!-- Filters -->
    <div class="grid grid-cols-1 gap-3 sm:grid-cols-4">
      <FormField :label="t('settings.audit.entityType')">
        <AppInput v-model="entityType" :placeholder="t('settings.audit.entityTypePlaceholder')" />
      </FormField>
      <FormField :label="t('settings.audit.from')">
        <input v-model="fromDate" type="date" class="field-input" />
      </FormField>
      <FormField :label="t('settings.audit.to')">
        <input v-model="toDate" type="date" class="field-input" />
      </FormField>
      <div class="flex items-end gap-2">
        <AppButton :disabled="loading" @click="applyFilters">{{ t('settings.audit.apply') }}</AppButton>
        <AppButton variant="ghost" :disabled="loading" @click="load">
          <RefreshCw :size="16" :class="{ 'animate-spin': loading }" />
        </AppButton>
      </div>
    </div>

    <p v-if="error" class="text-sm text-negative">{{ error }}</p>

    <div v-if="loading" class="text-sm text-text-muted">{{ t('common.loading') }}</div>

    <div
      v-else-if="!rows.length"
      class="flex items-center gap-3 rounded-lg border border-border bg-surface-2 px-4 py-6 text-sm text-text-muted"
    >
      <FileSearch :size="18" />
      {{ t('settings.audit.empty') }}
    </div>

    <template v-else>
      <div class="overflow-hidden rounded-lg border border-border">
        <table class="w-full text-sm">
          <thead class="bg-surface-2 text-left text-xs text-text-muted">
            <tr>
              <th class="w-8 px-2 py-2"></th>
              <th class="px-3 py-2 font-medium">{{ t('settings.audit.when') }}</th>
              <th class="px-3 py-2 font-medium">{{ t('settings.audit.action') }}</th>
              <th class="px-3 py-2 font-medium">{{ t('settings.audit.entity') }}</th>
              <th class="px-3 py-2 font-medium">{{ t('settings.audit.user') }}</th>
            </tr>
          </thead>
          <tbody>
            <template v-for="row in rows" :key="row.id">
              <tr
                class="cursor-pointer border-t border-border align-top hover:bg-surface-2"
                @click="toggle(row.id)"
              >
                <td class="px-2 py-2 text-text-muted">
                  <ChevronRight
                    :size="15"
                    class="transition-transform"
                    :class="{ 'rotate-90': expanded === row.id }"
                  />
                </td>
                <td class="whitespace-nowrap px-3 py-2 text-text-muted">{{ formatWhen(row.timestampUtc) }}</td>
                <td class="px-3 py-2">
                  <StatusBadge :tone="actionTone[row.action]" :label="t(`settings.audit.actions.${row.action}`)" />
                </td>
                <td class="px-3 py-2">
                  <span class="font-medium text-text">{{ row.entityType }}</span>
                  <span class="ml-1 font-mono text-xs text-text-muted" :title="row.entityId">
                    {{ shortId(row.entityId) }}
                  </span>
                </td>
                <td class="px-3 py-2 font-mono text-xs text-text-muted" :title="row.userId">
                  {{ shortId(row.userId) }}
                </td>
              </tr>
              <tr v-if="expanded === row.id" class="border-t border-border bg-surface-2">
                <td colspan="5" class="px-4 py-3">
                  <table class="w-full text-xs">
                    <thead class="text-left text-text-muted">
                      <tr>
                        <th class="py-1 pr-4 font-medium">{{ t('settings.audit.field') }}</th>
                        <th class="py-1 pr-4 font-medium">{{ t('settings.audit.before') }}</th>
                        <th class="py-1 font-medium">{{ t('settings.audit.after') }}</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr v-for="c in parseChanges(row)" :key="c.field" class="align-top">
                        <td class="py-1 pr-4 font-medium text-text">{{ c.field }}</td>
                        <td class="py-1 pr-4 text-text-muted">
                          <span v-if="c.isUpdate" class="break-all">{{ c.before }}</span>
                          <span v-else class="text-text-muted/60">—</span>
                        </td>
                        <td class="break-all py-1 text-text">{{ c.after }}</td>
                      </tr>
                    </tbody>
                  </table>
                </td>
              </tr>
            </template>
          </tbody>
        </table>
      </div>

      <!-- Pagination -->
      <div class="flex items-center justify-between text-sm text-text-muted">
        <span>{{ t('common.showingRange', { from: rangeFrom, to: rangeTo, total: totalItems }) }}</span>
        <div class="flex items-center gap-2">
          <AppButton variant="ghost" :disabled="page <= 1 || loading" @click="goPage(page - 1)">
            {{ t('common.prev') }}
          </AppButton>
          <AppButton variant="ghost" :disabled="page >= totalPages || loading" @click="goPage(page + 1)">
            {{ t('common.next') }}
          </AppButton>
        </div>
      </div>
    </template>
  </div>
</template>
