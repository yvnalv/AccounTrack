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
}
