// Mirrors the ProcessTracker module's timeline DTO
// (GET /api/v1/documents/{documentType}/{documentId}/timeline).

export interface ProcessEventDto {
  id: string
  milestone: string
  note: string | null
  actorUserId: string | null
  occurredAtUtc: string
}

/** Document types the approval/process-tracker engine records against. */
export type DocumentType = 'SalesOrder' | 'PurchaseOrder' | 'ExpenseVoucher'
