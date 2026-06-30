import { http, unwrap } from './api'
import { getAuthToken, downloadExport, type ExportFormat } from './api'
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
  ProductCategory,
  Supplier,
  TaxCode,
  UnitOfMeasure,
  Warehouse,
} from '@/types/masterdata'

export const masterData = {
  customers: () => unwrap<Customer[]>(http.get('/customers')),
  suppliers: () => unwrap<Supplier[]>(http.get('/suppliers')),
  warehouses: () => unwrap<Warehouse[]>(http.get('/warehouses')),
  products: () => unwrap<Product[]>(http.get('/products')),
  unitsOfMeasure: () => unwrap<UnitOfMeasure[]>(http.get('/units-of-measure')),
  productCategories: () => unwrap<ProductCategory[]>(http.get('/product-categories')),
  taxCodes: () => unwrap<TaxCode[]>(http.get('/tax-codes')),

  createCustomer: (body: CreateCustomer) => unwrap<string>(http.post('/customers', body)),
  createSupplier: (body: CreateSupplier) => unwrap<string>(http.post('/suppliers', body)),
  createWarehouse: (body: CreateWarehouse) => unwrap<string>(http.post('/warehouses', body)),
  createProduct: (body: CreateProduct) => unwrap<string>(http.post('/products', body)),
  createUom: (body: { code: string; name: string }) => unwrap<string>(http.post('/units-of-measure', body)),
  createCategory: (body: { code: string; name: string }) => unwrap<string>(http.post('/product-categories', body)),
  createTaxCode: (body: { code: string; name: string; rate: number }) => unwrap<string>(http.post('/tax-codes', body)),

  // Edits (code is the immutable natural key, so it isn't part of the update body). The optional
  // rowVersion is echoed back for optimistic-concurrency checking (ADR-0021).
  updateCustomer: (id: string, body: Omit<CreateCustomer, 'code'>, rowVersion?: string | null) =>
    unwrap<string>(http.put(`/customers/${id}`, { ...body, rowVersion: rowVersion ?? null })),
  updateSupplier: (id: string, body: Omit<CreateSupplier, 'code'>, rowVersion?: string | null) =>
    unwrap<string>(http.put(`/suppliers/${id}`, { ...body, rowVersion: rowVersion ?? null })),
  updateWarehouse: (id: string, body: Omit<CreateWarehouse, 'code'>, rowVersion?: string | null) =>
    unwrap<string>(http.put(`/warehouses/${id}`, { ...body, rowVersion: rowVersion ?? null })),
  updateProduct: (id: string, body: Omit<CreateProduct, 'code' | 'baseUomId'>, rowVersion?: string | null) =>
    unwrap<string>(http.put(`/products/${id}`, { ...body, rowVersion: rowVersion ?? null })),
  updateUom: (id: string, body: { name: string }, rowVersion?: string | null) =>
    unwrap<string>(http.put(`/units-of-measure/${id}`, { ...body, rowVersion: rowVersion ?? null })),
  updateCategory: (id: string, body: { name: string }, rowVersion?: string | null) =>
    unwrap<string>(http.put(`/product-categories/${id}`, { ...body, rowVersion: rowVersion ?? null })),
  updateTaxCode: (id: string, body: { name: string; rate: number }, rowVersion?: string | null) =>
    unwrap<string>(http.put(`/tax-codes/${id}`, { ...body, rowVersion: rowVersion ?? null })),

  setCustomerActive: (id: string, isActive: boolean) =>
    unwrap<string>(http.put(`/customers/${id}/active`, { isActive })),
  setSupplierActive: (id: string, isActive: boolean) =>
    unwrap<string>(http.put(`/suppliers/${id}/active`, { isActive })),
  setWarehouseActive: (id: string, isActive: boolean) =>
    unwrap<string>(http.put(`/warehouses/${id}/active`, { isActive })),
  setProductActive: (id: string, isActive: boolean) =>
    unwrap<string>(http.put(`/products/${id}/active`, { isActive })),
  setUomActive: (id: string, isActive: boolean) =>
    unwrap<string>(http.put(`/units-of-measure/${id}/active`, { isActive })),
  setCategoryActive: (id: string, isActive: boolean) =>
    unwrap<string>(http.put(`/product-categories/${id}/active`, { isActive })),
  setTaxCodeActive: (id: string, isActive: boolean) =>
    unwrap<string>(http.put(`/tax-codes/${id}/active`, { isActive })),

  // CSV import/export (ADR-0031). One helper set per entity, same shape.
  customerImport: csvIo('customers'),
  supplierImport: csvIo('suppliers'),
  warehouseImport: csvIo('warehouses'),
  productImport: csvIo('products'),
}

/** Builds the preview/commit/export/template calls for a master-data entity's CSV endpoints. */
function csvIo(entity: string) {
  return {
    preview: (file: File) => unwrap<ImportPreview>(http.post(`/${entity}/import/preview`, toForm(file))),
    commit: (file: File) => unwrap<ImportCommit>(http.post(`/${entity}/import/commit`, toForm(file))),
    export: (format: ExportFormat = 'xlsx') => downloadExport(`/${entity}/export`, entity, format),
    template: () => downloadCsv(`/${entity}/import/template`, `${entity}-template.csv`),
  }
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
