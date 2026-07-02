export interface ExpenseCategory {
  id: string
  code: string
  name: string
  postingRuleKey: string
  isActive: boolean
}

export interface ExpenseVoucherSummary {
  id: string
  number: string
  expenseDate: string
  payeeName: string | null
  supplierId: string | null
  grandTotal: number
  journalEntryId: string | null
  status: string
}

export interface ExpenseLineInput {
  expenseCategoryId: string
  description: string | null
  amount: number
  taxRate: number
}

export interface ExpenseVoucherLine {
  expenseCategoryId: string
  description: string | null
  amount: number
  taxRate: number
  lineTax: number
  lineTotal: number
}

export interface ExpenseVoucher {
  id: string
  number: string
  expenseDate: string
  payeeName: string | null
  cashAccountId: string | null
  supplierId: string | null
  dueDate: string | null
  currency: string
  subTotal: number
  taxTotal: number
  grandTotal: number
  journalEntryId: string | null
  apOpenItemId: string | null
  reversalJournalEntryId: string | null
  status: string
  reference: string | null
  notes: string | null
  lines: ExpenseVoucherLine[]
}

export interface CreateExpenseVoucher {
  expenseDate: string
  payeeName: string | null
  cashAccountId: string | null
  supplierId: string | null
  dueDate: string | null
  reference: string | null
  notes: string | null
  lines: ExpenseLineInput[]
}

export interface SaveExpenseCategory {
  code: string
  name: string
  postingRuleKey: string
}
