import { http, unwrap } from './api'
import type { Permission, Role, SaveRole } from '@/types/roles'

export const rolesApi = {
  list: () => unwrap<Role[]>(http.get('/roles')),
  permissions: () => unwrap<Permission[]>(http.get('/permissions')),
  create: (body: SaveRole) => unwrap<string>(http.post('/roles', body)),
  update: (id: string, body: SaveRole) => unwrap<void>(http.put(`/roles/${id}`, body)),
  remove: (id: string) => unwrap<void>(http.delete(`/roles/${id}`)),
}
