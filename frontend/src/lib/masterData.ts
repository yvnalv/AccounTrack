import { http, unwrap } from './api'
import type { NamedRef, Product } from '@/types/masterdata'

export const masterData = {
  customers: () => unwrap<NamedRef[]>(http.get('/customers')),
  warehouses: () => unwrap<NamedRef[]>(http.get('/warehouses')),
  products: () => unwrap<Product[]>(http.get('/products')),
}

/** Builds an id → name map for resolving foreign keys in tables. */
export function nameMap(items: NamedRef[]): Map<string, string> {
  return new Map(items.map((i) => [i.id, i.name]))
}
