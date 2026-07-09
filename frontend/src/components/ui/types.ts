export interface Column {
  key: string
  label: string
  align?: 'left' | 'right'
  /** tabular numerics for money/qty columns */
  numeric?: boolean
  /** Hide this column on small screens (secondary info) — keeps mobile tables readable. */
  hideOnMobile?: boolean
}

export interface SelectOption {
  value: string
  label: string
}
