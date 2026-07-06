import { http, unwrap } from './api'
import type { NotificationDto } from '@/types/api'

export const notificationApi = {
  /** The current user's notifications, newest first (GET /notifications). */
  list: (unreadOnly = false) =>
    unwrap<NotificationDto[]>(http.get('/notifications/', { params: { unreadOnly } })),
  /** Marks one notification read (idempotent server-side). */
  markRead: (id: string) => unwrap<null>(http.post(`/notifications/${id}/read`)),
}
