// id-ID number/money formatting (DESIGN_LANGUAGE §2). Negatives render in parentheses.

const idID = (min = 0, max = 0) =>
  new Intl.NumberFormat('id-ID', { minimumFractionDigits: min, maximumFractionDigits: max })

export function formatNumber(value: number, fractionDigits = 0): string {
  return idID(fractionDigits, fractionDigits).format(value)
}

/** Money with the company currency code, negatives in parentheses (accounting convention). */
export function formatMoney(value: number, currency = 'IDR', fractionDigits = 0): string {
  const abs = idID(fractionDigits, fractionDigits).format(Math.abs(value))
  const body = `${currency} ${abs}`
  return value < 0 ? `(${body})` : body
}

export function formatPercent(value: number, fractionDigits = 1): string {
  return `${idID(fractionDigits, fractionDigits).format(value)}%`
}
