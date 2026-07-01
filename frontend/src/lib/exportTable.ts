import type { Column } from '@/components/ui/types'
import { getAuthToken, type ExportFormat } from './api'

/**
 * Exports the given rows as CSV/XLSX honoring the visible columns. Callers pass the rows the table is
 * currently showing (already filtered/searched client-side), so the download reflects the active
 * filters (ADR-0031). The server just renders the caller's own data — no extra data is exposed.
 */
export async function exportTable(
  columns: Column[],
  rows: Record<string, unknown>[],
  fileName: string,
  format: ExportFormat = 'xlsx',
): Promise<void> {
  const cols = columns.filter((c) => c.key !== 'actions')
  const header = cols.map((c) => c.label)
  const body = rows.map((r) => cols.map((c) => cell(r[c.key])))

  const res = await fetch(`/api/v1/export?format=${format}`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${getAuthToken() ?? ''}`,
    },
    body: JSON.stringify({ fileName, header, rows: body }),
  })

  const blob = await res.blob()
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `${fileName}.${format}`
  a.click()
  URL.revokeObjectURL(url)
}

/** Cells are exported as raw values (numbers/enums), not their formatted display, which suits data export. */
function cell(value: unknown): string | null {
  if (value == null) return null
  return String(value)
}
