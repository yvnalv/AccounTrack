import { http, unwrap } from './api'
import type {
  CreateExpenseVoucher,
  ExpenseCategory,
  ExpenseVoucherSummary,
  SaveExpenseCategory,
} from '@/types/expenses'

export const expensesApi = {
  categories: () => unwrap<ExpenseCategory[]>(http.get('/expense-categories')),
  createCategory: (body: SaveExpenseCategory) => unwrap<string>(http.post('/expense-categories', body)),
  updateCategory: (id: string, body: Omit<SaveExpenseCategory, 'code'>) =>
    unwrap<void>(http.put(`/expense-categories/${id}`, body)),
  setCategoryActive: (id: string, isActive: boolean) =>
    unwrap<void>(http.put(`/expense-categories/${id}/active`, { isActive })),
  vouchers: () => unwrap<ExpenseVoucherSummary[]>(http.get('/expense-vouchers')),
  createVoucher: (body: CreateExpenseVoucher) => unwrap<string>(http.post('/expense-vouchers', body)),
}
