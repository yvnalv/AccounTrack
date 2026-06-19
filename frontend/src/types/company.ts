// Mirrors CompanyManagement CompanyDto + update contract.

export interface Company {
  id: string
  code: string
  name: string
  legalName: string | null
  functionalCurrency: string
  fiscalYearStartMonth: number
  timeZone: string
  taxId: string | null
  isActive: boolean
}

export interface UpdateCompany {
  name: string
  legalName: string | null
  taxId: string | null
  timeZone: string
}
