import { http, unwrap } from './api'
import type {
  BalanceSheet,
  CashFlowStatement,
  GeneralLedger,
  ProfitAndLoss,
  TrialBalance,
  VatReport,
} from '@/types/reports'

interface DateRange {
  fromDate?: string
  toDate?: string
}

export const reportsApi = {
  trialBalance: (range: DateRange = {}) =>
    unwrap<TrialBalance>(http.get('/reports/trial-balance', { params: range })),
  profitLoss: (range: DateRange = {}) =>
    unwrap<ProfitAndLoss>(http.get('/reports/profit-loss', { params: range })),
  balanceSheet: (asOfDate?: string) =>
    unwrap<BalanceSheet>(http.get('/reports/balance-sheet', { params: asOfDate ? { asOfDate } : {} })),
  vat: (range: DateRange = {}) =>
    unwrap<VatReport>(http.get('/reports/vat', { params: range })),
  cashFlow: (range: DateRange = {}) =>
    unwrap<CashFlowStatement>(http.get('/reports/cash-flow', { params: range })),
  generalLedger: (params: DateRange & { accountId?: string } = {}) =>
    unwrap<GeneralLedger>(http.get('/reports/general-ledger', { params })),
}
