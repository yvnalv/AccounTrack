import { http, unwrap } from './api'
import type {
  InventoryValuation,
  StockCardEntry,
  StockOnHand,
  StockOpnameResult,
  TransferStockResult,
} from '@/types/inventory'

export interface AdjustStockPayload {
  productId: string
  warehouseId: string
  quantity: number
  increase: boolean
  unitCost: number | null
  date: string
  reason: string
}

export interface StockOpnamePayload {
  productId: string
  warehouseId: string
  countedQuantity: number
  unitCost: number | null
  date: string
  notes: string | null
}

export interface TransferStockPayload {
  productId: string
  fromWarehouseId: string
  toWarehouseId: string
  quantity: number
  date: string
}

export const inventoryApi = {
  onHand: () => unwrap<StockOnHand[]>(http.get('/stock/on-hand')),
  stockCard: (productId: string, warehouseId?: string) =>
    unwrap<StockCardEntry[]>(
      http.get('/stock/card', { params: warehouseId ? { productId, warehouseId } : { productId } }),
    ),
  adjust: (payload: AdjustStockPayload) => unwrap(http.post('/stock/adjustments', payload)),
  opname: (payload: StockOpnamePayload) =>
    unwrap<StockOpnameResult>(http.post('/stock/opname', payload)),
  transfer: (payload: TransferStockPayload) =>
    unwrap<TransferStockResult>(http.post('/stock/transfers', payload)),
  valuation: () => unwrap<InventoryValuation>(http.get('/stock/valuation')),
}

/** Movement types that increase stock (the rest decrease it). */
const INBOUND = new Set(['Receipt', 'AdjustmentIn', 'TransferIn', 'ProductionReceive'])
export const isInbound = (type: string) => INBOUND.has(type)
