<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { processTrackerApi } from '@/lib/processTracker'
import { timeAgo } from '@/lib/format'
import type { DocumentType, ProcessEventDto } from '@/types/processTracker'
import AppCard from '@/components/ui/AppCard.vue'

const props = defineProps<{ documentType: DocumentType; documentId: string }>()

const { t, locale } = useI18n()

const events = ref<ProcessEventDto[]>([])
const loading = ref(true)
const error = ref('')

// Milestones are recorded in English by the consumer; map the known ones to i18n keys,
// falling back to the raw milestone text for anything not (yet) translated.
const milestoneKey: Record<string, string> = {
  'Submitted for approval': 'timeline.milestones.submitted',
  'Auto-approved': 'timeline.milestones.autoApproved',
  Approved: 'timeline.milestones.approved',
  'Approval advanced': 'timeline.milestones.advanced',
  Rejected: 'timeline.milestones.rejected',
}

function label(milestone: string): string {
  const key = milestoneKey[milestone]
  return key ? t(key) : milestone
}

function absoluteTime(iso: string): string {
  const d = new Date(iso)
  return Number.isNaN(d.getTime())
    ? iso
    : d.toLocaleString(locale.value, { dateStyle: 'medium', timeStyle: 'short' })
}

function shortId(id: string): string {
  return id.length > 8 ? id.slice(0, 8) : id
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    events.value = await processTrackerApi.timeline(props.documentType, props.documentId)
  } catch {
    error.value = t('timeline.loadFailed')
  } finally {
    loading.value = false
  }
}

onMounted(load)
watch(() => props.documentId, load)
</script>

<template>
  <AppCard :title="t('timeline.title')">
    <p v-if="loading" class="text-sm text-text-muted">{{ t('common.loading') }}</p>
    <p v-else-if="error" class="text-sm text-negative">{{ error }}</p>
    <p v-else-if="!events.length" class="text-sm text-text-muted">{{ t('timeline.empty') }}</p>

    <ol v-else class="space-y-0">
      <li v-for="(e, i) in events" :key="e.id" class="relative flex gap-3 pb-5 last:pb-0">
        <!-- Rail -->
        <div class="flex flex-col items-center">
          <span class="mt-1 h-2.5 w-2.5 shrink-0 rounded-full bg-accent" />
          <span v-if="i < events.length - 1" class="w-px flex-1 bg-border" />
        </div>
        <!-- Content -->
        <div class="-mt-0.5 min-w-0 flex-1">
          <p class="text-sm font-medium text-text">{{ label(e.milestone) }}</p>
          <p class="mt-0.5 text-xs text-text-muted">
            <span :title="absoluteTime(e.occurredAtUtc)">{{ timeAgo(e.occurredAtUtc, locale) }}</span>
            <template v-if="e.actorUserId">
              · <span class="font-mono" :title="e.actorUserId">{{ shortId(e.actorUserId) }}</span>
            </template>
          </p>
          <p v-if="e.note" class="mt-0.5 text-xs text-text-muted">{{ e.note }}</p>
        </div>
      </li>
    </ol>
  </AppCard>
</template>
