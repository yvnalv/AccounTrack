import { http, unwrap } from './api'
import type { AccountRef, SubledgerOpenItem } from '@/types/accounting'

export const accountingApi = {
  /** Open (unsettled) AR items for a customer. */
  arOpenItems: (partyId: string) =>
    unwrap<SubledgerOpenItem[]>(http.get('/ar/open-items', { params: { partyId } })),
  /** Open (unsettled) AP items for a supplier. */
  apOpenItems: (partyId: string) =>
    unwrap<SubledgerOpenItem[]>(http.get('/ap/open-items', { params: { partyId } })),
  accounts: () => unwrap<AccountRef[]>(http.get('/accounts')),
}

/** Cash/bank GL accounts (code band 10xx, postable) — what a payment can deposit to. */
export function cashAccounts(accounts: AccountRef[]): AccountRef[] {
  return accounts.filter((a) => a.isActive && a.allowPosting && a.code.startsWith('10'))
}
