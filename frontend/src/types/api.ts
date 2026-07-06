// Mirrors the backend API response envelope and DTOs we consume.

export interface ApiSuccess<T> {
  success: true
  data: T
}

export interface ApiFailure {
  success: false
  message: string
}

export type ApiResponse<T> = ApiSuccess<T> | ApiFailure

export interface AuthResponse {
  accessToken: string
  accessTokenExpiresAtUtc: string
  refreshToken: string
  refreshTokenExpiresAtUtc: string
  userId: string
  email: string
  fullName: string
  roles: string[]
  permissions: string[]
  companyIds: string[]
}

export interface NotificationDto {
  id: string
  title: string
  body: string
  isRead: boolean
  readAtUtc: string | null
  createdAt: string
}

export interface DashboardMonthlyPoint {
  month: string
  revenue: number
  expense: number
  profit: number
}

export interface DashboardAging {
  current: number
  days1To30: number
  days31To60: number
  days61To90: number
  days90Plus: number
}

export interface DashboardNamedAmount {
  name: string
  amount: number
}

export interface DashboardSummary {
  currency: string
  asOfDate: string
  cashAndBank: number
  accountsReceivable: number
  accountsPayable: number
  overdueReceivable: number
  overduePayable: number
  revenueThisMonth: number
  expenseThisMonth: number
  netProfitThisMonth: number
  revenuePrevMonth: number
  expensePrevMonth: number
  inventoryValue: number
  overdueReceivableCount: number
  overduePayableCount: number
  monthlyTrend: DashboardMonthlyPoint[]
  arAging: DashboardAging
  apAging: DashboardAging
  expenseByCategory: DashboardNamedAmount[]
  topReceivables: DashboardNamedAmount[]
  topPayables: DashboardNamedAmount[]
}
