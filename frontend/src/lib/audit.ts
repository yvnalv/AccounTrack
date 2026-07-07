import { http, unwrap } from './api'
import type { AuditEntryDto, AuditFilter, Paged } from '@/types/audit'

export const auditApi = {
  /** Lists audit entries for the current tenant, filtered + paged, newest first (Audit.View). */
  list: (filter: AuditFilter = {}) =>
    unwrap<Paged<AuditEntryDto>>(
      http.get('/audit-entries', {
        params: {
          entityType: filter.entityType || undefined,
          entityId: filter.entityId || undefined,
          fromUtc: filter.fromUtc || undefined,
          toUtc: filter.toUtc || undefined,
          page: filter.page ?? 1,
          pageSize: filter.pageSize ?? 50,
        },
      }),
    ),
}
