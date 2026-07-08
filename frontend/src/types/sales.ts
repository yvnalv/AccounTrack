export type SalesOrderStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Approved'
  | 'Rejected'
  | 'Cancelled'
  | 'PartiallyDelivered'
  | 'Delivered'

export interface SalesOrderSummary {
  id: string
  number: string
  customerId: string
  status: SalesOrderStatus
  grandTotal: number
  orderDate: string
}

export interface SalesOrderLine {
  id: string
  productId: string
  quantity: number
  unitPrice: number
  taxRate: number
  lineSubTotal: number
  lineTaxAmount: number
  lineTotal: number
  description: string | null
  deliveredQuantity: number
  invoicedQuantity: number
  outstandingQuantity: number
}

export interface DeliverySummary {
  id: string
  number: string
  salesOrderId: string
  deliveryDate: string
  totalCost: number
  journalEntryId: string | null
}

export interface DeliveryListItem {
  id: string
  number: string
  deliveryDate: string
  salesOrderId: string
  customerId: string
  customerName: string
  totalCost: number
  journalEntryId: string | null
}

export interface CustomerPaymentSummary {
  id: string
  number: string
  customerId: string
  paymentDate: string
  totalAmount: number
  journalEntryId: string | null
}

export interface DeliveryOrderLine {
  salesOrderLineId: string
  productId: string
  quantity: number
  unitCost: number
  lineCost: number
}

export interface DeliveryOrder {
  id: string
  number: string
  salesOrderId: string
  customerId: string
  warehouseId: string
  currency: string
  deliveryDate: string
  totalCost: number
  journalEntryId: string | null
  notes: string | null
  lines: DeliveryOrderLine[]
}

export interface CustomerPaymentAllocation {
  arOpenItemId: string
  amount: number
}

export interface CustomerPayment {
  id: string
  number: string
  customerId: string
  cashAccountId: string
  currency: string
  paymentDate: string
  totalAmount: number
  journalEntryId: string | null
  reference: string | null
  notes: string | null
  allocations: CustomerPaymentAllocation[]
}

export interface SalesInvoiceSummary {
  id: string
  number: string
  salesOrderId: string
  invoiceDate: string
  dueDate: string
  grandTotal: number
  journalEntryId: string | null
}

/** A row in the company-wide Sales Invoices list (customer name resolved). */
export interface SalesInvoiceListItem {
  id: string
  number: string
  salesOrderId: string
  customerId: string
  customerName: string
  invoiceDate: string
  dueDate: string
  grandTotal: number
  journalEntryId: string | null
}

/** A row in the company-wide Customer Payments list (customer name resolved). */
export interface CustomerPaymentListItem {
  id: string
  number: string
  customerId: string
  customerName: string
  paymentDate: string
  totalAmount: number
  journalEntryId: string | null
}

export interface LineQuantityInput {
  salesOrderLineId: string
  quantity: number
}

export interface SalesInvoiceLine {
  id: string
  salesOrderLineId: string
  productId: string
  quantity: number
  unitPrice: number
  taxRate: number
  lineNet: number
  lineTax: number
  lineTotal: number
  returnableQuantity: number
}

export interface SalesInvoice {
  id: string
  number: string
  salesOrderId: string
  customerId: string
  currency: string
  invoiceDate: string
  dueDate: string
  subTotal: number
  taxTotal: number
  grandTotal: number
  journalEntryId: string | null
  arOpenItemId: string | null
  notes: string | null
  lines: SalesInvoiceLine[]
}

export interface SalesReturnSummary {
  id: string
  number: string
  salesInvoiceId: string
  returnDate: string
  grandTotal: number
  journalEntryId: string | null
}

export interface SalesReturnListItem {
  id: string
  number: string
  returnDate: string
  customerId: string
  customerName: string
  grandTotal: number
  journalEntryId: string | null
}

export interface SalesReturnLine {
  salesInvoiceLineId: string
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

export interface SalesReturn {
  id: string
  number: string
  salesInvoiceId: string
  salesOrderId: string
  customerId: string
  warehouseId: string
  currency: string
  returnDate: string
  subTotal: number
  taxTotal: number
  grandTotal: number
  totalCost: number
  journalEntryId: string | null
  notes: string | null
  lines: SalesReturnLine[]
}

export interface ReturnLineInput {
  salesInvoiceLineId: string
  quantity: number
}

export interface CustomerPaymentAllocationInput {
  arOpenItemId: string
  amount: number
}

export interface CreateCustomerPayment {
  customerId: string
  cashAccountId: string
  paymentDate: string
  reference: string | null
  notes: string | null
  allocations: CustomerPaymentAllocationInput[]
}

export interface SalesOrder {
  id: string
  number: string
  customerId: string
  warehouseId: string
  currency: string
  orderDate: string
  status: SalesOrderStatus
  approvalRequestId: string | null
  subTotal: number
  taxTotal: number
  grandTotal: number
  notes: string | null
  lines: SalesOrderLine[]
  /** Optimistic-concurrency token (base64); echo it back on update to detect stale edits. */
  rowVersion: string | null
}

export interface CreateSalesOrderLine {
  productId: string
  quantity: number
  unitPrice: number
  taxRate: number
  description: string | null
}

export interface CreateSalesOrder {
  customerId: string
  warehouseId: string
  orderDate: string
  notes: string | null
  lines: CreateSalesOrderLine[]
}
