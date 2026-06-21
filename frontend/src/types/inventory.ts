export interface StockOnHand {
  productId: string
  warehouseId: string
  onHandQty: number
  avgUnitCost: number
  value: number
  currency: string
}

export interface StockOpnameResult {
  systemQty: number
  countedQty: number
  variance: number
  transactionId: string | null
  costApplied: number
}

export interface InventoryValuationRow {
  productId: string
  productName: string
  quantity: number
  avgUnitCost: number
  value: number
}

export interface InventoryValuation {
  currency: string
  rows: InventoryValuationRow[]
  totalValue: number
  glInventoryBalance: number
  difference: number
  isReconciled: boolean
}

export interface StockCardEntry {
  transactionId: string
  date: string
  type: string
  quantity: number
  unitCost: number
  totalCost: number
  runningQtyAfter: number
  runningAvgCostAfter: number
  source: string
  sourceDocumentId: string | null
  description: string | null
}
