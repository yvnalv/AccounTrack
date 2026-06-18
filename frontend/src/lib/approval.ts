import { http, unwrap } from './api'
import type { ApprovalRequest } from '@/types/approval'

export const approvalApi = {
  mine: () => unwrap<ApprovalRequest[]>(http.get('/approval-requests/mine')),
  approve: (id: string, comment: string | null) =>
    unwrap<string>(http.post(`/approval-requests/${id}/approve`, { comment })),
  reject: (id: string, comment: string | null) =>
    unwrap<string>(http.post(`/approval-requests/${id}/reject`, { comment })),
}
