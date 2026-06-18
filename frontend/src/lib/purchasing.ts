import { http, unwrap } from './api'
import type {
  CreatePurchaseOrder,
  CreateSupplierPayment,
  GoodsReceiptSummary,
  PoLineQuantityInput,
  PurchaseInvoiceSummary,
  PurchaseOrder,
  PurchaseOrderSummary,
} from '@/types/purchasing'

export const purchasingApi = {
  list: () => unwrap<PurchaseOrderSummary[]>(http.get('/purchase-orders')),
  get: (id: string) => unwrap<PurchaseOrder>(http.get(`/purchase-orders/${id}`)),
  create: (body: CreatePurchaseOrder) => unwrap<string>(http.post('/purchase-orders', body)),
  submit: (id: string) => unwrap<string>(http.post(`/purchase-orders/${id}/submit`, {})),

  receipts: (id: string) =>
    unwrap<GoodsReceiptSummary[]>(http.get(`/purchase-orders/${id}/goods-receipts`)),
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
}
