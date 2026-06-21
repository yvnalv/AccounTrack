export interface Role {
  id: string
  name: string
  description: string | null
  isSystem: boolean
  isAdministrator: boolean
  userCount: number
  permissions: string[]
}

export interface Permission {
  code: string
  name: string
  module: string
}

export interface SaveRole {
  name: string
  description: string | null
  permissions: string[]
}
