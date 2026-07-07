// Mirrors the AuditLog module's read DTOs (GET /api/v1/audit-entries, Audit.View).

export type AuditAction = 'Insert' | 'Update' | 'Delete'

export interface AuditEntryDto {
  id: string
  tenantId: string
  companyId: string | null
  entityType: string
  entityId: string
  action: AuditAction
  /** JSON document: snapshot for Insert/Delete, `{ field: { old, new } }` for Update. */
  changesJson: string
  userId: string
  timestampUtc: string
}

export interface AuditFilter {
  entityType?: string | null
  entityId?: string | null
  fromUtc?: string | null
  toUtc?: string | null
  page?: number
  pageSize?: number
}

/** The API_SPEC §2 paged-collection envelope (SharedKernel PagedResult<T>). */
export interface Paged<T> {
  items: T[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
}
