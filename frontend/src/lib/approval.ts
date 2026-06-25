import { http, unwrap } from './api'
import type { ApprovalRequest, DeadLetterEvent } from '@/types/approval'

export const approvalApi = {
  mine: () => unwrap<ApprovalRequest[]>(http.get('/approval-requests/mine')),
  approve: (id: string, comment: string | null) =>
    unwrap<string>(http.post(`/approval-requests/${id}/approve`, { comment })),
  reject: (id: string, comment: string | null) =>
    unwrap<string>(http.post(`/approval-requests/${id}/reject`, { comment })),
  deadLetters: () => unwrap<DeadLetterEvent[]>(http.get('/approval/outbox/dead-letter')),
  retryDeadLetter: (id: string) =>
    unwrap<null>(http.post(`/approval/outbox/dead-letter/${id}/retry`)),
}
