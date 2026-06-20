import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ImportCommit, ImportPreview } from '@/types/masterdata'

export interface CsvIo {
  preview: (file: File) => Promise<ImportPreview>
  commit: (file: File) => Promise<ImportCommit | string>
  export: () => Promise<void>
  template: () => Promise<void>
}

/** Shared CSV-import state machine (ADR-0031): pick file → dry-run preview → all-or-nothing commit. */
export function useCsvImport(io: CsvIo, reload: () => Promise<void>) {
  const { t } = useI18n()
  const fileInput = ref<HTMLInputElement | null>(null)
  const open = ref(false)
  const preview = ref<ImportPreview | null>(null)
  const file = ref<File | null>(null)
  const busy = ref(false)
  const error = ref('')

  const canCommit = computed(
    () => !!preview.value && preview.value.errorRows === 0 && preview.value.totalRows > 0,
  )

  function pick() {
    fileInput.value?.click()
  }

  async function onFileChosen(e: Event) {
    const input = e.target as HTMLInputElement
    const f = input.files?.[0]
    input.value = '' // allow re-selecting the same file
    if (!f) return
    file.value = f
    error.value = ''
    preview.value = null
    busy.value = true
    open.value = true
    try {
      preview.value = await io.preview(f)
    } catch {
      error.value = t('masterData.import.failed')
    } finally {
      busy.value = false
    }
  }

  async function commit() {
    if (!file.value) return
    busy.value = true
    error.value = ''
    try {
      await io.commit(file.value)
      open.value = false
      await reload()
    } catch {
      error.value = t('masterData.import.failed')
    } finally {
      busy.value = false
    }
  }

  return { fileInput, open, preview, busy, error, canCommit, pick, onFileChosen, commit }
}
