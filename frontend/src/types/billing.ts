// Billing — how the tenant manages its own Accountrack subscription (SUBSCRIPTION_BILLING.md).
// Money is IDR minor units, which for IDR equals whole rupiah, so it can be formatted directly.

export type BillingInterval = 'Monthly' | 'Annual'

export type SubscriptionStatus =
  | 'Trialing'
  | 'Active'
  | 'PastDue'
  | 'Canceled'
  | 'Unpaid'
  | 'Expired'

export type PaymentMode = 'AutoCharge' | 'Invoice' | 'Manual'

export type BillingInvoiceStatus = 'Draft' | 'Open' | 'Paid' | 'Void' | 'Uncollectible'

export type TenantAccessLevel = 'Full' | 'ReadOnly' | 'Locked'

export interface Plan {
  id: string
  code: string
  name: string
  interval: BillingInterval
  basePriceMinor: number
  includedSeats: number
  perSeatPriceMinor: number
  maxCompanies: number | null
  currency: string
  featuresJson: string
  isActive: boolean
  isPublic: boolean
}

export interface Subscription {
  id: string
  planId: string
  planCode: string
  planName: string
  status: SubscriptionStatus
  interval: BillingInterval
  extraSeats: number
  paymentMode: PaymentMode
  trialEndsAt: string | null
  currentPeriodStart: string | null
  currentPeriodEnd: string | null
  cancelAtPeriodEnd: boolean
}

export interface Entitlements {
  hasSubscription: boolean
  accessLevel: TenantAccessLevel
  planCode: string | null
  planName: string | null
  status: SubscriptionStatus | null
  trialEndsAt: string | null
  currentPeriodEnd: string | null
  maxUsers: number | null
  maxCompanies: number | null
  features: Record<string, boolean>
}

export interface Checkout {
  billingInvoiceId: string
  payUrl: string
  amountMinor: number
  currency: string
}

export interface BillingInvoice {
  id: string
  number: string
  periodStart: string
  periodEnd: string
  totalMinor: number
  currency: string
  status: BillingInvoiceStatus
  dueDate: string
  paidAt: string | null
}
