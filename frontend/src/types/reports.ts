export interface TrialBalanceRow {
  accountCode: string
  accountName: string
  accountType: string
  debit: number
  credit: number
  balance: number
}

export interface TrialBalance {
  fromDate: string | null
  toDate: string | null
  totalDebit: number
  totalCredit: number
  isBalanced: boolean
  lines: TrialBalanceRow[]
}

export interface ReportLine {
  accountCode: string
  accountName: string
  amount: number
}

export interface ProfitAndLoss {
  fromDate: string | null
  toDate: string | null
  revenue: ReportLine[]
  totalRevenue: number
  expenses: ReportLine[]
  totalExpenses: number
  netProfit: number
}

export interface VatReport {
  fromDate: string | null
  toDate: string | null
  outputAccountCode: string
  outputAccountName: string
  outputTax: number
  inputAccountCode: string
  inputAccountName: string
  inputTax: number
  netVatPayable: number
}

export interface CashFlowSection {
  lines: ReportLine[]
  total: number
}

export interface CashFlowStatement {
  fromDate: string | null
  toDate: string | null
  netIncome: number
  operating: CashFlowSection
  investing: CashFlowSection
  financing: CashFlowSection
  netChangeInCash: number
  openingCash: number
  closingCash: number
  isReconciled: boolean
}

export interface BalanceSheet {
  asOfDate: string
  assets: ReportLine[]
  totalAssets: number
  liabilities: ReportLine[]
  totalLiabilities: number
  equity: ReportLine[]
  currentEarnings: number
  totalEquity: number
  totalLiabilitiesAndEquity: number
  isBalanced: boolean
}
