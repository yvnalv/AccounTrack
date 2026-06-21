export interface User {
  id: string
  email: string
  fullName: string
  isActive: boolean
  lastLoginAtUtc: string | null
  roleIds: string[]
  companyIds: string[]
}

export interface CreateUser {
  email: string
  password: string
  fullName: string
  roleIds: string[]
  companyIds: string[]
}

export interface UpdateUser {
  fullName: string
  roleIds: string[]
  companyIds: string[]
}
