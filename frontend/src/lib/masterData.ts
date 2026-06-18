import { http, unwrap } from './api'
import type {
  CreateCustomer,
  CreateProduct,
  CreateSupplier,
  CreateWarehouse,
  Customer,
  NamedRef,
  Product,
  Supplier,
  Warehouse,
} from '@/types/masterdata'

export const masterData = {
  customers: () => unwrap<Customer[]>(http.get('/customers')),
  suppliers: () => unwrap<Supplier[]>(http.get('/suppliers')),
  warehouses: () => unwrap<Warehouse[]>(http.get('/warehouses')),
  products: () => unwrap<Product[]>(http.get('/products')),
  unitsOfMeasure: () => unwrap<NamedRef[]>(http.get('/units-of-measure')),
  productCategories: () => unwrap<NamedRef[]>(http.get('/product-categories')),

  createCustomer: (body: CreateCustomer) => unwrap<string>(http.post('/customers', body)),
  createSupplier: (body: CreateSupplier) => unwrap<string>(http.post('/suppliers', body)),
  createWarehouse: (body: CreateWarehouse) => unwrap<string>(http.post('/warehouses', body)),
  createProduct: (body: CreateProduct) => unwrap<string>(http.post('/products', body)),
}

/** Builds an id → name map for resolving foreign keys in tables. */
export function nameMap(items: NamedRef[]): Map<string, string> {
  return new Map(items.map((i) => [i.id, i.name]))
}
