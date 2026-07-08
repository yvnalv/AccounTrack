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

export interface SupplierPaymentSummary {
  id: string
  number: string
  supplierId: string
  paymentDate: string
  totalAmount: number
  journalEntryId: string | null
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
  /** Optimistic-concurrency token (base64); echo it back on update to detect stale edits. */
  rowVersion: string | null
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

export interface GoodsReceiptLine {
  purchaseOrderLineId: string
  productId: string
  quantity: number
  unitCost: number
  lineCost: number
}

export interface GoodsReceipt {
  id: string
  number: string
  purchaseOrderId: string
  supplierId: string
  warehouseId: string
  currency: string
  receiptDate: string
  totalCost: number
  journalEntryId: string | null
  notes: string | null
  lines: GoodsReceiptLine[]
}

export interface SupplierPaymentAllocation {
  apOpenItemId: string
  amount: number
}

export interface SupplierPayment {
  id: string
  number: string
  supplierId: string
  cashAccountId: string
  currency: string
  paymentDate: string
  totalAmount: number
  journalEntryId: string | null
  reference: string | null
  notes: string | null
  allocations: SupplierPaymentAllocation[]
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

export interface PurchaseInvoiceLine {
  id: string
  purchaseOrderLineId: string
  productId: string
  quantity: number
  unitPrice: number
  taxRate: number
  lineNet: number
  lineTax: number
  lineTotal: number
  returnableQuantity: number
}

export interface PurchaseInvoice {
  id: string
  number: string
  supplierInvoiceNo: string | null
  purchaseOrderId: string
  supplierId: string
  currency: string
  invoiceDate: string
  dueDate: string
  subTotal: number
  taxTotal: number
  grandTotal: number
  journalEntryId: string | null
  apOpenItemId: string | null
  notes: string | null
  lines: PurchaseInvoiceLine[]
}

export interface PurchaseReturnSummary {
  id: string
  number: string
  purchaseInvoiceId: string
  returnDate: string
  grandTotal: number
  journalEntryId: string | null
}

export interface PurchaseReturnListItem {
  id: string
  number: string
  returnDate: string
  supplierId: string
  supplierName: string
  grandTotal: number
  journalEntryId: string | null
}

export interface PurchaseReturnLine {
  purchaseInvoiceLineId: string
  productId: string
  quantity: number
  unitPrice: number
  taxRate: number
  unitCost: number
  lineNet: number
  lineTax: number
  lineTotal: number
  lineCost: number
}

export interface PurchaseReturn {
  id: string
  number: string
  purchaseInvoiceId: string
  purchaseOrderId: string
  supplierId: string
  warehouseId: string
  currency: string
  returnDate: string
  subTotal: number
  taxTotal: number
  grandTotal: number
  totalCost: number
  journalEntryId: string | null
  notes: string | null
  lines: PurchaseReturnLine[]
}

export interface ReturnLineInput {
  purchaseInvoiceLineId: string
  quantity: number
}

export interface SupplierPaymentAllocationInput {
  apOpenItemId: string
  amount: number
}

export interface CreateSupplierPayment {
  supplierId: string
  cashAccountId: string
  paymentDate: string
  reference: string | null
  notes: string | null
  allocations: SupplierPaymentAllocationInput[]
}
