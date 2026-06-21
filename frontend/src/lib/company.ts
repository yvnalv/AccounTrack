import { http, unwrap } from './api'
import type { Company, UpdateCompany } from '@/types/company'

export const companyApi = {
  list: () => unwrap<Company[]>(http.get('/companies')),
  get: (id: string) => unwrap<Company>(http.get(`/companies/${id}`)),
  update: (id: string, body: UpdateCompany) => unwrap<unknown>(http.put(`/companies/${id}`, body)),
  setSetting: (id: string, key: string, value: string) =>
    unwrap<unknown>(http.put(`/companies/${id}/settings`, { key, value })),
}
