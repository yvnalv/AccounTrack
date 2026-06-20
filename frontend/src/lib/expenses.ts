import { http, unwrap } from './api'
import type { CreateExpenseVoucher, ExpenseCategory, ExpenseVoucherSummary } from '@/types/expenses'

export const expensesApi = {
  categories: () => unwrap<ExpenseCategory[]>(http.get('/expense-categories')),
  vouchers: () => unwrap<ExpenseVoucherSummary[]>(http.get('/expense-vouchers')),
  createVoucher: (body: CreateExpenseVoucher) => unwrap<string>(http.post('/expense-vouchers', body)),
}
