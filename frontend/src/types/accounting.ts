/** An AR or AP subledger open item (same shape for both). */
export interface SubledgerOpenItem {
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

/** One party's outstanding, split into aging buckets. */
export interface AgingRow {
  partyId: string
  current: number
  days1To30: number
  days31To60: number
  days61To90: number
  days90Plus: number
  total: number
}

/** AR/AP aging report: per-party rows plus company totals. */
export interface AgingReport {
  type: string
  asOfDate: string
  rows: AgingRow[]
  current: number
  days1To30: number
  days31To60: number
  days61To90: number
  days90Plus: number
  total: number
}

export interface AccountRef {
  id: string
  code: string
  name: string
  type: string
  normalBalance?: string
  isControlAccount?: boolean
  controlType?: string
  allowPosting: boolean
  isActive: boolean
  isSystem?: boolean
  rowVersion?: string | null
}

export interface FiscalPeriod {
  id: string
  periodNo: number
  startDate: string
  endDate: string
  status: string
}

export interface FiscalYear {
  id: string
  year: number
  startDate: string
  endDate: string
  isClosed: boolean
  periods: FiscalPeriod[]
}

export interface CloseFiscalYearResult {
  journalEntryId: string | null
  netIncome: number
}

export interface PeriodBalance {
  accountCode: string
  accountName: string
  debit: number
  credit: number
}
