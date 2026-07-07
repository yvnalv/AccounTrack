import { http, unwrap } from './api'
import type { DocumentType, ProcessEventDto } from '@/types/processTracker'

export const processTrackerApi = {
  /** A document's lifecycle timeline, oldest first (any authenticated user). */
  timeline: (documentType: DocumentType, documentId: string) =>
    unwrap<ProcessEventDto[]>(http.get(`/documents/${documentType}/${documentId}/timeline`)),
}
