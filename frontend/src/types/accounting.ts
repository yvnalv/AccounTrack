export interface ArOpenItem {
  id: string
  documentNo: string
  documentDate: string
  dueDate: string
  currency: string
  originalAmount: number
  settledAmount: number
  outstandingAmount: number
  status: string
}

export interface AccountRef {
  id: string
  code: string
  name: string
  type: string
  allowPosting: boolean
  isActive: boolean
}
