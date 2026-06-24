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
