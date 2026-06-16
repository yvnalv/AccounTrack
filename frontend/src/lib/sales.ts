import { http, unwrap } from './api'
import type { CreateSalesOrder, SalesOrder, SalesOrderSummary } from '@/types/sales'

export const salesApi = {
  list: () => unwrap<SalesOrderSummary[]>(http.get('/sales-orders')),
  get: (id: string) => unwrap<SalesOrder>(http.get(`/sales-orders/${id}`)),
  create: (body: CreateSalesOrder) => unwrap<string>(http.post('/sales-orders', body)),
  submit: (id: string) => unwrap<string>(http.post(`/sales-orders/${id}/submit`, {})),
}
