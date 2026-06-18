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
