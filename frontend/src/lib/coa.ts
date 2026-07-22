import type { AccountRef } from '@/types/accounting'
import type { Locale } from '@/i18n'

/**
 * Localized display names for the **standard (seeded) chart of accounts**. The account name is
 * editable data stored per company, so we only translate an account whose name is still the seeded
 * English default — a user's own rename is always shown verbatim. Keyed by account code; `en` must
 * match the seeded name exactly (AccountingDataSeeder / docs/POSTING_RULES.md §2).
 */
const COA_NAMES: Record<string, { en: string; id: string }> = {
  '1000': { en: 'Cash', id: 'Kas' },
  '1010': { en: 'Bank', id: 'Bank' },
  '1100': { en: 'Accounts Receivable', id: 'Piutang Usaha' },
  '1200': { en: 'Inventory', id: 'Persediaan' },
  '1300': { en: 'VAT Input (PPN Masukan)', id: 'PPN Masukan' },
  '1400': { en: 'Supplier Advances', id: 'Uang Muka Pemasok' },
  '2100': { en: 'Accounts Payable', id: 'Utang Usaha' },
  '2150': { en: 'Goods Received / Invoice Received', id: 'Barang Diterima / Faktur Diterima' },
  '2300': { en: 'VAT Output (PPN Keluaran)', id: 'PPN Keluaran' },
  '2400': { en: 'Customer Advances', id: 'Uang Muka Pelanggan' },
  '2500': { en: 'Bank Loan Payable', id: 'Utang Bank' },
  '2600': { en: 'Dividends Payable', id: 'Utang Dividen' },
  '3000': { en: "Owner's Capital (Modal Pemilik)", id: 'Modal Pemilik' },
  '3100': { en: 'Additional Paid-in Capital (Agio Saham)', id: 'Agio Saham' },
  '3200': { en: "Owner's Drawings (Prive)", id: 'Prive Pemilik' },
  '3300': { en: 'Share Capital (Modal Saham)', id: 'Modal Saham' },
  '3400': { en: 'Dividends Declared', id: 'Dividen Diumumkan' },
  '3900': { en: 'Retained Earnings', id: 'Laba Ditahan' },
  '3950': { en: 'Opening Balance Equity', id: 'Ekuitas Saldo Awal' },
  '4000': { en: 'Sales Revenue', id: 'Pendapatan Penjualan' },
  '5000': { en: 'Cost of Goods Sold', id: 'Harga Pokok Penjualan' },
  '5100': { en: 'Inventory Variance', id: 'Selisih Persediaan' },
  '6000': { en: 'Electricity & Utilities', id: 'Listrik & Utilitas' },
  '6100': { en: 'Transportation', id: 'Transportasi' },
  '6200': { en: 'Rent', id: 'Sewa' },
  '6300': { en: 'Office Supplies', id: 'Perlengkapan Kantor' },
  '6400': { en: 'Salaries & Wages', id: 'Gaji & Upah' },
  '6900': { en: 'Other Operating Expense', id: 'Beban Operasional Lain' },
  '7900': { en: 'Rounding Difference', id: 'Selisih Pembulatan' },
}

/** The account's display name in the active locale — the seeded default translated, or a user rename verbatim. */
export function localizedAccountName(account: Pick<AccountRef, 'code' | 'name'>, locale: Locale): string {
  const std = COA_NAMES[account.code]
  return std && account.name === std.en ? std[locale] : account.name
}

/** A `code · name` label for account dropdowns, localized. */
export function accountOptionLabel(account: Pick<AccountRef, 'code' | 'name'>, locale: Locale): string {
  return `${account.code} · ${localizedAccountName(account, locale)}`
}
