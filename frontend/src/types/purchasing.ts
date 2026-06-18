export type PurchaseOrderStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Approved'
  | 'Rejected'
  | 'Cancelled'
  | 'PartiallyReceived'
  | 'Received'

export interface PurchaseOrderSummary {
  id: string
  number: string
  supplierId: string
  status: PurchaseOrderStatus
  grandTotal: number
  orderDate: string
}

export interface PurchaseOrderLine {
  id: string
  productId: string
  quantity: number
  unitPrice: number
  taxRate: number
  lineSubTotal: number
  lineTaxAmount: number
  lineTotal: number
  description: string | null
  receivedQuantity: number
  invoicedQuantity: number
  outstandingQuantity: number
}

export interface PurchaseOrder {
  id: string
  number: string
  supplierId: string
  warehouseId: string
  currency: string
  orderDate: string
  status: PurchaseOrderStatus
  approvalRequestId: string | null
  subTotal: number
  taxTotal: number
  grandTotal: number
  notes: string | null
  lines: PurchaseOrderLine[]
}

export interface CreatePurchaseOrder {
  supplierId: string
  warehouseId: string
  orderDate: string
  notes: string | null
  lines: { productId: string; quantity: number; unitPrice: number; taxRate: number; description: string | null }[]
}

export interface GoodsReceiptSummary {
  id: string
  number: string
  purchaseOrderId: string
  receiptDate: string
  totalCost: number
  journalEntryId: string | null
}

export interface PurchaseInvoiceSummary {
  id: string
  number: string
  purchaseOrderId: string
  invoiceDate: string
  dueDate: string
  grandTotal: number
  journalEntryId: string | null
}

export interface PoLineQuantityInput {
  purchaseOrderLineId: string
  quantity: number
}
