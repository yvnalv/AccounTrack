export interface ApprovalStep {
  level: number
  approverType: string
  approverRef: string
}

export interface ApprovalAction {
  level: number
  approverId: string
  decision: string
  comment: string | null
  actedAtUtc: string
}

export interface ApprovalRequest {
  id: string
  documentType: string
  documentId: string
  status: string
  currentLevel: number
  maxLevel: number
  submittedBy: string
  steps: ApprovalStep[]
  actions: ApprovalAction[]
}

/** A failed (dead-lettered) integration event the outbox dispatcher gave up on. */
export interface DeadLetterEvent {
  id: string
  eventType: string
  occurredOnUtc: string
  attempts: number
  error: string | null
}
