// Cross-module dashboard insights (from Sales / Purchasing insight endpoints).
export interface MonthlyAmount {
  month: string // "yyyy-MM"
  amount: number
}

export interface NamedAmount {
  name: string
  amount: number
}

export interface SalesInsights {
  monthlySales: MonthlyAmount[]
  topCustomers: NamedAmount[]
  topProducts: NamedAmount[]
  salesByCategory: NamedAmount[]
}

export interface PurchasingInsights {
  monthlyPurchases: MonthlyAmount[]
  topSuppliers: NamedAmount[]
}
