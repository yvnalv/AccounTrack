import { http, unwrap } from './api'
import type { CreateUser, UpdateUser, User } from '@/types/users'

export const usersApi = {
  list: () => unwrap<User[]>(http.get('/users')),
  create: (body: CreateUser) => unwrap<string>(http.post('/users', body)),
  update: (id: string, body: UpdateUser) => unwrap<void>(http.put(`/users/${id}`, body)),
  setActive: (id: string, isActive: boolean) =>
    unwrap<void>(http.put(`/users/${id}/active`, { isActive })),
}
