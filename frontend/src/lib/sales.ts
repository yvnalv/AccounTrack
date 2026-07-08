import { http, unwrap } from './api'
import type {
  CreateCustomerPayment,
  CreateSalesOrder,
  CustomerPayment,
  CustomerPaymentListItem,
  DeliveryListItem,
  DeliveryOrder,
  DeliverySummary,
  LineQuantityInput,
  ReturnLineInput,
  SalesInvoice,
  SalesInvoiceListItem,
  SalesInvoiceSummary,
  SalesOrder,
  SalesOrderSummary,
  SalesReturn,
  SalesReturnListItem,
  SalesReturnSummary,
} from '@/types/sales'

export const salesApi = {
  list: () => unwrap<SalesOrderSummary[]>(http.get('/sales-orders')),
  get: (id: string) => unwrap<SalesOrder>(http.get(`/sales-orders/${id}`)),
  create: (body: CreateSalesOrder) => unwrap<string>(http.post('/sales-orders', body)),
  update: (id: string, body: CreateSalesOrder, rowVersion?: string | null) =>
    unwrap<string>(http.put(`/sales-orders/${id}`, { ...body, rowVersion: rowVersion ?? null })),
  submit: (id: string) => unwrap<string>(http.post(`/sales-orders/${id}/submit`, {})),
  cancel: (id: string) => unwrap<string>(http.post(`/sales-orders/${id}/cancel`, {})),

  deliveries: (id: string) =>
    unwrap<DeliverySummary[]>(http.get(`/sales-orders/${id}/deliveries`)),
  allDeliveries: () => unwrap<DeliveryListItem[]>(http.get('/delivery-orders')),
  getDelivery: (deliveryId: string) => unwrap<DeliveryOrder>(http.get(`/delivery-orders/${deliveryId}`)),
  allInvoices: () => unwrap<SalesInvoiceListItem[]>(http.get('/sales-invoices')),
  customerPayments: (customerId: string) =>
    unwrap<CustomerPaymentListItem[]>(http.get('/customer-payments', { params: { customerId } })),
  allCustomerPayments: () => unwrap<CustomerPaymentListItem[]>(http.get('/customer-payments')),
  getCustomerPayment: (paymentId: string) =>
    unwrap<CustomerPayment>(http.get(`/customer-payments/${paymentId}`)),
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

  getInvoice: (invoiceId: string) => unwrap<SalesInvoice>(http.get(`/sales-invoices/${invoiceId}`)),
  returns: (id: string) => unwrap<SalesReturnSummary[]>(http.get(`/sales-orders/${id}/returns`)),
  allReturns: () => unwrap<SalesReturnListItem[]>(http.get('/sales-returns')),
  getReturn: (returnId: string) => unwrap<SalesReturn>(http.get(`/sales-returns/${returnId}`)),
  createReturn: (
    invoiceId: string,
    body: {
      returnDate: string
      notes: string | null
      lines: ReturnLineInput[]
      refundCashAccountId?: string | null
    },
  ) => unwrap<string>(http.post(`/sales-invoices/${invoiceId}/returns`, body)),
}
