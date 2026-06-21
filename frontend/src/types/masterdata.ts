export interface NamedRef {
  id: string
  code: string
  name: string
}

export interface Product extends NamedRef {
  baseUomId: string
  categoryId: string | null
  isStockTracked: boolean
  isSold: boolean
  isPurchased: boolean
  isActive: boolean
}

export interface Customer extends NamedRef {
  taxId: string | null
  paymentTermDays: number
  creditLimit: number
  isActive: boolean
}

export interface Supplier extends NamedRef {
  taxId: string | null
  paymentTermDays: number
  isActive: boolean
}

export interface Warehouse extends NamedRef {
  address: string | null
  isActive: boolean
}

export interface UnitOfMeasure extends NamedRef {
  isActive: boolean
}

export interface ProductCategory extends NamedRef {
  isActive: boolean
}

export interface TaxCode extends NamedRef {
  rate: number
  isActive: boolean
}

export interface CreateProduct {
  code: string
  name: string
  baseUomId: string
  categoryId: string | null
  isStockTracked: boolean
  isSold: boolean
  isPurchased: boolean
}

export interface CreateCustomer {
  code: string
  name: string
  taxId: string | null
  paymentTermDays: number
  creditLimit: number
}

export interface CreateSupplier {
  code: string
  name: string
  taxId: string | null
  paymentTermDays: number
}

export interface CreateWarehouse {
  code: string
  name: string
  address: string | null
}

export type ImportRowAction = 'Create' | 'Update' | 'Error'

export interface ImportRowResult {
  rowNumber: number
  action: ImportRowAction
  key: string | null
  name: string | null
  errors: string[]
}

export interface ImportPreview {
  totalRows: number
  toCreate: number
  toUpdate: number
  errorRows: number
  rows: ImportRowResult[]
}

export interface ImportCommit {
  committed: boolean
  created: number
  updated: number
  errorRows: number
  rows: ImportRowResult[]
}
