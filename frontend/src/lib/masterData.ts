import { http, unwrap } from './api'
import { getAuthToken } from './api'
import type {
  CreateCustomer,
  CreateProduct,
  CreateSupplier,
  CreateWarehouse,
  Customer,
  ImportCommit,
  ImportPreview,
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

  // Edits (code is the immutable natural key, so it isn't part of the update body).
  updateCustomer: (id: string, body: Omit<CreateCustomer, 'code'>) =>
    unwrap<string>(http.put(`/customers/${id}`, body)),
  updateSupplier: (id: string, body: Omit<CreateSupplier, 'code'>) =>
    unwrap<string>(http.put(`/suppliers/${id}`, body)),
  updateWarehouse: (id: string, body: Omit<CreateWarehouse, 'code'>) =>
    unwrap<string>(http.put(`/warehouses/${id}`, body)),
  updateProduct: (id: string, body: Omit<CreateProduct, 'code' | 'baseUomId'>) =>
    unwrap<string>(http.put(`/products/${id}`, body)),

  setCustomerActive: (id: string, isActive: boolean) =>
    unwrap<string>(http.put(`/customers/${id}/active`, { isActive })),
  setSupplierActive: (id: string, isActive: boolean) =>
    unwrap<string>(http.put(`/suppliers/${id}/active`, { isActive })),
  setWarehouseActive: (id: string, isActive: boolean) =>
    unwrap<string>(http.put(`/warehouses/${id}/active`, { isActive })),
  setProductActive: (id: string, isActive: boolean) =>
    unwrap<string>(http.put(`/products/${id}/active`, { isActive })),

  // CSV import/export (ADR-0031).
  previewCustomerImport: (file: File) => unwrap<ImportPreview>(http.post('/customers/import/preview', toForm(file))),
  commitCustomerImport: (file: File) => unwrap<ImportCommit>(http.post('/customers/import/commit', toForm(file))),
  exportCustomers: () => downloadCsv('/customers/export', 'customers.csv'),
  customerImportTemplate: () => downloadCsv('/customers/import/template', 'customers-template.csv'),
}

function toForm(file: File): FormData {
  const fd = new FormData()
  fd.append('file', file)
  return fd
}

/** Streams a CSV endpoint to a browser download (carries the bearer token). */
async function downloadCsv(path: string, fileName: string): Promise<void> {
  const res = await fetch(`/api/v1${path}`, {
    headers: { Authorization: `Bearer ${getAuthToken() ?? ''}` },
  })
  const blob = await res.blob()
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = fileName
  a.click()
  URL.revokeObjectURL(url)
}

/** Builds an id → name map for resolving foreign keys in tables. */
export function nameMap(items: NamedRef[]): Map<string, string> {
  return new Map(items.map((i) => [i.id, i.name]))
}
