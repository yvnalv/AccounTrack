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
