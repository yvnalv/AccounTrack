import { http, unwrap } from './api'
import type {
  CreatePurchaseOrder,
  CreateSupplierPayment,
  GoodsReceipt,
  GoodsReceiptSummary,
  SupplierPayment,
  SupplierPaymentSummary,
  PoLineQuantityInput,
  PurchaseInvoice,
  PurchaseInvoiceSummary,
  PurchaseOrder,
  PurchaseOrderSummary,
  PurchaseReturn,
  PurchaseReturnListItem,
  PurchaseReturnSummary,
  ReturnLineInput,
} from '@/types/purchasing'

export const purchasingApi = {
  list: () => unwrap<PurchaseOrderSummary[]>(http.get('/purchase-orders')),
  supplierPayments: (supplierId: string) =>
    unwrap<SupplierPaymentSummary[]>(http.get('/supplier-payments', { params: { supplierId } })),
  get: (id: string) => unwrap<PurchaseOrder>(http.get(`/purchase-orders/${id}`)),
  create: (body: CreatePurchaseOrder) => unwrap<string>(http.post('/purchase-orders', body)),
  update: (id: string, body: CreatePurchaseOrder, rowVersion?: string | null) =>
    unwrap<string>(http.put(`/purchase-orders/${id}`, { ...body, rowVersion: rowVersion ?? null })),
  submit: (id: string) => unwrap<string>(http.post(`/purchase-orders/${id}/submit`, {})),
  cancel: (id: string) => unwrap<string>(http.post(`/purchase-orders/${id}/cancel`, {})),

  receipts: (id: string) =>
    unwrap<GoodsReceiptSummary[]>(http.get(`/purchase-orders/${id}/goods-receipts`)),
  getReceipt: (receiptId: string) => unwrap<GoodsReceipt>(http.get(`/goods-receipts/${receiptId}`)),
  createReceipt: (id: string, body: { receiptDate: string; notes: string | null; lines: PoLineQuantityInput[] }) =>
    unwrap<string>(http.post(`/purchase-orders/${id}/goods-receipts`, body)),

  invoices: (id: string) =>
    unwrap<PurchaseInvoiceSummary[]>(http.get(`/purchase-orders/${id}/invoices`)),
  createInvoice: (
    id: string,
    body: { supplierInvoiceNo: string | null; invoiceDate: string; dueDate: string; notes: string | null; lines: PoLineQuantityInput[] },
  ) => unwrap<string>(http.post(`/purchase-orders/${id}/invoices`, body)),

  createSupplierPayment: (body: CreateSupplierPayment) =>
    unwrap<string>(http.post('/supplier-payments', body)),
  getSupplierPayment: (paymentId: string) =>
    unwrap<SupplierPayment>(http.get(`/supplier-payments/${paymentId}`)),

  getInvoice: (invoiceId: string) => unwrap<PurchaseInvoice>(http.get(`/purchase-invoices/${invoiceId}`)),
  returns: (id: string) => unwrap<PurchaseReturnSummary[]>(http.get(`/purchase-orders/${id}/returns`)),
  allReturns: () => unwrap<PurchaseReturnListItem[]>(http.get('/purchase-returns')),
  getReturn: (returnId: string) => unwrap<PurchaseReturn>(http.get(`/purchase-returns/${returnId}`)),
  createReturn: (
    invoiceId: string,
    body: {
      returnDate: string
      notes: string | null
      lines: ReturnLineInput[]
      refundCashAccountId?: string | null
    },
  ) => unwrap<string>(http.post(`/purchase-invoices/${invoiceId}/returns`, body)),
}
