export interface Column {
  key: string
  label: string
  align?: 'left' | 'right'
  /** tabular numerics for money/qty columns */
  numeric?: boolean
}

export interface SelectOption {
  value: string
  label: string
}
