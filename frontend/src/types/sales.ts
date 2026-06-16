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
  outstandingQuantity: number
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
