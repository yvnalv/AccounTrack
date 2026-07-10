export interface Column {
  key: string
  label: string
  align?: 'left' | 'right'
  /** tabular numerics for money/qty columns */
  numeric?: boolean
  /** Hide this column on small screens (secondary info) — keeps mobile tables readable. */
  hideOnMobile?: boolean
  /** Disable click-to-sort for this column. Sorting is on by default for every column except
   *  `actions`; the row value at `key` is used unless `sortValue` is provided. */
  sortable?: boolean
  /** Custom value to sort a row by for this column (e.g. when the cell renders a formatted/derived
   *  string but the underlying sortable value lives elsewhere on the row). */
  sortValue?: (row: Record<string, unknown>) => string | number | null | undefined
}

export interface SelectOption {
  value: string
  label: string
}
