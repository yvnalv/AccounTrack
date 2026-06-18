import { http, unwrap } from './api'
import type { BalanceSheet, ProfitAndLoss, TrialBalance } from '@/types/reports'

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
}
