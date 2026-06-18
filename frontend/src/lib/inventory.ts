import { http, unwrap } from './api'
import type { StockCardEntry, StockOnHand } from '@/types/inventory'

export const inventoryApi = {
  onHand: () => unwrap<StockOnHand[]>(http.get('/stock/on-hand')),
  stockCard: (productId: string, warehouseId?: string) =>
    unwrap<StockCardEntry[]>(
      http.get('/stock/card', { params: warehouseId ? { productId, warehouseId } : { productId } }),
    ),
}

/** Movement types that increase stock (the rest decrease it). */
const INBOUND = new Set(['Receipt', 'AdjustmentIn', 'TransferIn', 'ProductionReceive'])
export const isInbound = (type: string) => INBOUND.has(type)
