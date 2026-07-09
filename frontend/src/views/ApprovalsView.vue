<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { Check, X } from 'lucide-vue-next'
import { approvalApi } from '@/lib/approval'
import type { ApprovalRequest } from '@/types/approval'
import AppButton from '@/components/ui/AppButton.vue'
import AppModal from '@/components/ui/AppModal.vue'
import DataTable from '@/components/ui/DataTable.vue'
import type { Column } from '@/components/ui/types'

const { t } = useI18n()
const rows = ref<ApprovalRequest[]>([])
const loading = ref(true)

const modalOpen = ref(false)
const decision = ref<'approve' | 'reject'>('approve')
const target = ref<ApprovalRequest | null>(null)
const comment = ref('')
const submitting = ref(false)
const error = ref('')

const columns = computed<Column[]>(() => [
  { key: 'document', label: t('approvals.columns.document') },
  { key: 'reference', label: t('approvals.columns.reference') },
  { key: 'level', label: t('approvals.columns.level'), hideOnMobile: true },
  { key: 'actions', label: '', align: 'right' },
])

function docLabel(type: string) {
  const key = `approvals.docTypes.${type}`
  const label = t(key)
  return label === key ? type : label
}

const tableRows = computed(() =>
  rows.value.map((r) => ({
    ...r,
    document: docLabel(r.documentType),
    reference: r.documentId.slice(0, 8),
    level: `${r.currentLevel} / ${r.maxLevel}`,
  })),
)

async function load() {
  loading.value = true
  try {
    rows.value = await approvalApi.mine()
  } finally {
    loading.value = false
  }
}
onMounted(load)

function openDecision(req: ApprovalRequest, kind: 'approve' | 'reject') {
  target.value = req
  decision.value = kind
  comment.value = ''
  error.value = ''
  modalOpen.value = true
}

async function confirm() {
  if (!target.value) return
  submitting.value = true
  error.value = ''
  try {
    const fn = decision.value === 'approve' ? approvalApi.approve : approvalApi.reject
    await fn(target.value.id, comment.value || null)
    modalOpen.value = false
    await load()
  } catch {
    error.value = t('approvals.failed')
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <DataTable searchable :columns="columns" :rows="tableRows" :loading="loading" :empty-text="t('approvals.empty')">
    <template #cell-actions="{ row }">
      <div class="flex justify-end gap-2">
        <AppButton variant="secondary" @click="openDecision(row as unknown as ApprovalRequest, 'approve')">
          <Check :size="15" /> {{ t('approvals.approve') }}
        </AppButton>
        <AppButton variant="danger" @click="openDecision(row as unknown as ApprovalRequest, 'reject')">
          <X :size="15" /> {{ t('approvals.reject') }}
        </AppButton>
      </div>
    </template>
  </DataTable>

  <AppModal
    v-model="modalOpen"
    :title="decision === 'approve' ? t('approvals.confirmApprove') : t('approvals.confirmReject')"
  >
    <div class="space-y-3">
      <p class="text-sm text-text-muted">
        {{ docLabel(target?.documentType ?? '') }} · {{ target?.documentId.slice(0, 8) }}
      </p>
      <label class="block">
        <span class="mb-1.5 block text-sm font-medium text-text">{{ t('approvals.comment') }}</span>
        <textarea v-model="comment" rows="3" class="field-input h-auto py-2"></textarea>
      </label>
      <p v-if="error" class="text-sm text-negative">{{ error }}</p>
    </div>
    <template #footer>
      <AppButton variant="ghost" @click="modalOpen = false">{{ t('approvals.cancel') }}</AppButton>
      <AppButton :variant="decision === 'approve' ? 'primary' : 'danger'" :disabled="submitting" @click="confirm">
        {{ t('approvals.confirm') }}
      </AppButton>
    </template>
  </AppModal>
</template>
