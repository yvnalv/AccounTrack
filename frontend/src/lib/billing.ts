import { http, unwrap } from './api'
import type { BillingInvoice, Checkout, Entitlements, Plan, Subscription } from '@/types/billing'

/** Billing API client (SUBSCRIPTION_BILLING.md §9). */
export const billingApi = {
  plans: () => unwrap<Plan[]>(http.get('/billing/plans')),
  subscription: () => unwrap<Subscription | null>(http.get('/billing/subscription')),
  entitlements: () => unwrap<Entitlements>(http.get('/billing/entitlements')),
  invoices: () => unwrap<BillingInvoice[]>(http.get('/billing/invoices')),
  startTrial: (planCode: string) =>
    unwrap<string>(http.post('/billing/subscription/trial', { planCode })),
  // planCode optional: omit to bill the current plan, or pass a plan to upgrade/downgrade to.
  checkout: (planCode?: string) =>
    unwrap<Checkout>(http.post('/billing/subscription/checkout', planCode ? { planCode } : {})),
}
