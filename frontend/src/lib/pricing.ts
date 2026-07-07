import { http, unwrap } from './api'
import type { CreatePriceList, PriceList, PriceListItem, PriceListType } from '@/types/masterdata'

export const pricingApi = {
  list: () => unwrap<PriceList[]>(http.get('/price-lists')),
  create: (body: CreatePriceList) => unwrap<string>(http.post('/price-lists', body)),
  update: (id: string, body: { name: string; isDefault: boolean; isActive: boolean }, rowVersion?: string | null) =>
    unwrap<string>(http.put(`/price-lists/${id}`, { ...body, rowVersion: rowVersion ?? null })),

  items: (id: string) => unwrap<PriceListItem[]>(http.get(`/price-lists/${id}/items`)),
  upsertItem: (id: string, productId: string, unitPrice: number) =>
    unwrap<string>(http.put(`/price-lists/${id}/items`, { productId, unitPrice })),
  deleteItem: (id: string, productId: string) =>
    unwrap<string>(http.delete(`/price-lists/${id}/items/${productId}`)),

  /** Resolves productId → unit price for a party (company default overlaid by the party's list). */
  resolve: (type: PriceListType, partyId?: string | null) =>
    unwrap<Record<string, number>>(http.get('/price-lists/resolve', { params: { type, partyId: partyId || undefined } })),
}
