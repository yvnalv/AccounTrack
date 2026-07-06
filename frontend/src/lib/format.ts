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

/** Locale-aware relative time ("2 hours ago", "2 jam lalu"); falls back to the raw string if unparseable. */
export function timeAgo(iso: string, locale = 'en'): string {
  const then = new Date(iso).getTime()
  if (Number.isNaN(then)) return iso
  const diffSec = Math.round((then - Date.now()) / 1000)
  const rtf = new Intl.RelativeTimeFormat(locale, { numeric: 'auto' })
  const units: [Intl.RelativeTimeFormatUnit, number][] = [
    ['year', 31_536_000],
    ['month', 2_592_000],
    ['day', 86_400],
    ['hour', 3_600],
    ['minute', 60],
  ]
  for (const [unit, secs] of units) {
    if (Math.abs(diffSec) >= secs) {
      return rtf.format(Math.round(diffSec / secs), unit)
    }
  }
  return rtf.format(Math.round(diffSec), 'second')
}

/**
 * Compact money for KPI tiles where space is tight (e.g. "IDR 5,9B", "IDR 28M").
 * Uses K / M / B / T suffixes; below 1,000 it falls back to the full number.
 * Negatives render in parentheses (accounting convention).
 */
export function formatMoneyShort(value: number, currency = 'IDR'): string {
  const abs = Math.abs(value)
  const mantissa = (n: number) => idID(0, 1).format(n)
  let body: string
  if (abs >= 1e12) body = `${mantissa(abs / 1e12)}T`
  else if (abs >= 1e9) body = `${mantissa(abs / 1e9)}B`
  else if (abs >= 1e6) body = `${mantissa(abs / 1e6)}M`
  else if (abs >= 1e3) body = `${mantissa(abs / 1e3)}K`
  else body = idID(0, 0).format(abs)
  const full = `${currency} ${body}`
  return value < 0 ? `(${full})` : full
}
