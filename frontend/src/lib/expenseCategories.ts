import type { Locale } from '@/i18n'

/**
 * Localized display names for the **seeded default expense categories** (ExpensesSeeder). Like the
 * chart of accounts, a category name is editable data, so we only translate a category whose name is
 * still the seeded English default — a user's own rename is always shown verbatim. Keyed by code.
 */
const CATEGORY_NAMES: Record<string, { en: string; id: string }> = {
  ELECTRICITY: { en: 'Electricity & Utilities', id: 'Listrik & Utilitas' },
  TRANSPORT: { en: 'Transportation', id: 'Transportasi' },
  RENT: { en: 'Rent', id: 'Sewa' },
  SUPPLIES: { en: 'Office Supplies', id: 'Perlengkapan Kantor' },
  SALARIES: { en: 'Salaries & Wages', id: 'Gaji & Upah' },
  OTHER: { en: 'Other Operating Expense', id: 'Beban Operasional Lain' },
}

/** The expense category's display name in the active locale — seeded default translated, or a rename verbatim. */
export function localizedCategoryName(category: { code: string; name: string }, locale: Locale): string {
  const std = CATEGORY_NAMES[category.code]
  return std && category.name === std.en ? std[locale] : category.name
}
