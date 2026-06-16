export interface NamedRef {
  id: string
  code: string
  name: string
}

export interface Product extends NamedRef {
  isSold: boolean
  isActive: boolean
}
