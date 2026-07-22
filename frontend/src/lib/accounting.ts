import { http, unwrap } from './api'
import type {
  AccountRef,
  AgingReport,
  CloseFiscalYearResult,
  FiscalYear,
  JournalEntry,
  JournalRegisterItem,
  ManualJournalResult,
  PeriodBalance,
  SubledgerOpenItem,
} from '@/types/accounting'

/** A line posted to the manual-journal endpoint. */
export interface PostJournalLine {
  accountId: string
  debit: number
  credit: number
  description?: string | null
}

export const accountingApi = {
  /** Open (unsettled) AR items for a customer. */
  arOpenItems: (partyId: string) =>
    unwrap<SubledgerOpenItem[]>(http.get('/ar/open-items', { params: { partyId } })),
  /** Open (unsettled) AP items for a supplier. */
  apOpenItems: (partyId: string) =>
    unwrap<SubledgerOpenItem[]>(http.get('/ap/open-items', { params: { partyId } })),
  /** AR aging with a per-customer row (outstanding + overdue buckets). */
  arAging: () => unwrap<AgingReport>(http.get('/ar/aging')),
  /** AP aging with a per-supplier row (outstanding + overdue buckets). */
  apAging: () => unwrap<AgingReport>(http.get('/ap/aging')),
  accounts: () => unwrap<AccountRef[]>(http.get('/accounts')),
  createAccount: (body: { code: string; name: string; type: string; isControlAccount: boolean; controlType: string }) =>
    unwrap<string>(http.post('/accounts', body)),
  updateAccount: (id: string, body: { name: string; allowPosting: boolean }, rowVersion?: string | null) =>
    unwrap(http.put(`/accounts/${id}`, { ...body, rowVersion: rowVersion ?? null })),
  setAccountActive: (id: string, isActive: boolean) =>
    unwrap(http.put(`/accounts/${id}/active`, { isActive })),

  // Fiscal years & periods
  fiscalYears: () => unwrap<FiscalYear[]>(http.get('/fiscal-years')),
  createFiscalYear: (year: number, startMonth = 1) =>
    unwrap<string>(http.post('/fiscal-years', { year, startMonth })),
  closePeriod: (id: string) => unwrap(http.post(`/fiscal-periods/${id}/close`, {})),
  reopenPeriod: (id: string) => unwrap(http.post(`/fiscal-periods/${id}/reopen`, {})),
  periodBalances: (id: string) => unwrap<PeriodBalance[]>(http.get(`/fiscal-periods/${id}/balances`)),
  rebuildPeriodBalances: (id: string) => unwrap(http.post(`/fiscal-periods/${id}/balances/rebuild`, {})),
  closeFiscalYear: (id: string) =>
    unwrap<CloseFiscalYearResult>(http.post(`/fiscal-years/${id}/close`, {})),

  // General journal (ADR-0040)
  journalEntries: (params?: { fromDate?: string; toDate?: string }) =>
    unwrap<JournalRegisterItem[]>(http.get('/journal-entries', { params })),
  journalEntry: (id: string) => unwrap<JournalEntry>(http.get(`/journal-entries/${id}`)),
  postJournal: (body: { date: string; description: string; lines: PostJournalLine[] }) =>
    unwrap<ManualJournalResult>(http.post('/journal-entries', body)),
  reverseJournal: (id: string, body: { date?: string | null; reason?: string | null }) =>
    unwrap(http.post(`/journal-entries/${id}/reverse`, body)),

  // Guided Cash & Bank flows (ADR-0040)
  capitalContribution: (body: { date: string; amount: number; cashAccountId: string; equityAccountId?: string | null; memo?: string | null }) =>
    unwrap<ManualJournalResult>(http.post('/cash-bank/capital', body)),
  ownerDrawing: (body: { date: string; amount: number; cashAccountId: string; drawingsAccountId?: string | null; memo?: string | null }) =>
    unwrap<ManualJournalResult>(http.post('/cash-bank/owner-drawing', body)),
  bankTransfer: (body: { date: string; amount: number; fromAccountId: string; toAccountId: string; memo?: string | null }) =>
    unwrap<ManualJournalResult>(http.post('/cash-bank/transfer', body)),
  receiveMoney: (body: { date: string; amount: number; cashAccountId: string; creditAccountId: string; memo?: string | null }) =>
    unwrap<ManualJournalResult>(http.post('/cash-bank/receive', body)),
  spendMoney: (body: { date: string; amount: number; cashAccountId: string; debitAccountId: string; memo?: string | null }) =>
    unwrap<ManualJournalResult>(http.post('/cash-bank/spend', body)),
  loanReceipt: (body: { date: string; amount: number; cashAccountId: string; loanAccountId?: string | null; memo?: string | null }) =>
    unwrap<ManualJournalResult>(http.post('/cash-bank/loan-receipt', body)),
  loanRepayment: (body: { date: string; principal: number; interest: number; cashAccountId: string; loanAccountId?: string | null; interestAccountId?: string | null; memo?: string | null }) =>
    unwrap<ManualJournalResult>(http.post('/cash-bank/loan-repayment', body)),
}

/** Cash/bank GL accounts (code band 10xx, postable) — what a payment can deposit to. */
export function cashAccounts(accounts: AccountRef[]): AccountRef[] {
  return accounts.filter((a) => a.isActive && a.allowPosting && a.code.startsWith('10'))
}
