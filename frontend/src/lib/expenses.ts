import { http, unwrap } from './api'
import type {
  CreateExpenseVoucher,
  ExpenseCategory,
  ExpenseVoucher,
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
  getVoucher: (id: string) => unwrap<ExpenseVoucher>(http.get(`/expense-vouchers/${id}`)),
  // One-shot "save & post" (record + auto-post).
  createVoucher: (body: CreateExpenseVoucher) => unwrap<string>(http.post('/expense-vouchers', body)),
  // Draft workflow (parity with Sales/Purchasing).
  createDraft: (body: CreateExpenseVoucher) => unwrap<string>(http.post('/expense-vouchers/draft', body)),
  updateVoucher: (id: string, body: CreateExpenseVoucher, rowVersion?: string | null) =>
    unwrap<void>(http.put(`/expense-vouchers/${id}`, { ...body, rowVersion: rowVersion ?? null })),
  submitVoucher: (id: string) => unwrap<string>(http.post(`/expense-vouchers/${id}/submit`, {})),
  cancelVoucher: (id: string) => unwrap<void>(http.post(`/expense-vouchers/${id}/cancel`, {})),
  reverseVoucher: (id: string, body: { date?: string | null; reason?: string | null }) =>
    unwrap<string>(http.post(`/expense-vouchers/${id}/reverse`, body)),
}
