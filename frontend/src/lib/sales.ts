import { http, unwrap } from './api'
import type {
  CreateCustomerPayment,
  CreateSalesOrder,
  DeliverySummary,
  LineQuantityInput,
  SalesInvoiceSummary,
  SalesOrder,
  SalesOrderSummary,
} from '@/types/sales'

export const salesApi = {
  list: () => unwrap<SalesOrderSummary[]>(http.get('/sales-orders')),
  get: (id: string) => unwrap<SalesOrder>(http.get(`/sales-orders/${id}`)),
  create: (body: CreateSalesOrder) => unwrap<string>(http.post('/sales-orders', body)),
  submit: (id: string) => unwrap<string>(http.post(`/sales-orders/${id}/submit`, {})),

  deliveries: (id: string) =>
    unwrap<DeliverySummary[]>(http.get(`/sales-orders/${id}/deliveries`)),
  createDelivery: (id: string, body: { deliveryDate: string; notes: string | null; lines: LineQuantityInput[] }) =>
    unwrap<string>(http.post(`/sales-orders/${id}/deliveries`, body)),

  invoices: (id: string) =>
    unwrap<SalesInvoiceSummary[]>(http.get(`/sales-orders/${id}/invoices`)),
  createInvoice: (
    id: string,
    body: { invoiceDate: string; dueDate: string; notes: string | null; lines: LineQuantityInput[] },
  ) => unwrap<string>(http.post(`/sales-orders/${id}/invoices`, body)),

  createCustomerPayment: (body: CreateCustomerPayment) =>
    unwrap<string>(http.post('/customer-payments', body)),
}
