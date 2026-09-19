import type { FormLayoutColumn } from '@/features/form-layout/api'
import { formLayoutRoleNames } from '@/features/form-layout/format'

export interface FormHeaderChangeDto {
  ordinal: number
  was: string
  now: string
}

/** One Header Drift change enriched with the role and Column Label the ordinal carries. */
export interface FormHeaderChangeView {
  ordinal: number
  was: string
  now: string
  role: string | null
  label: string | null
  missing: boolean
}

/** Server sentinel: a drift change whose `now` is this value means the picked ordinal is gone from the file. */
export const columnNoLongerPresent = 'column no longer present'

export function describeHeaderChanges(
  changes: readonly FormHeaderChangeDto[],
  columns: readonly FormLayoutColumn[],
): FormHeaderChangeView[] {
  const columnByOrdinal = new Map(columns.map((column) => [column.ordinal, column]))
  return [...changes]
    .sort((left, right) => left.ordinal - right.ordinal)
    .map((change) => {
      const column = columnByOrdinal.get(change.ordinal)
      return {
        ordinal: change.ordinal,
        was: change.was,
        now: change.now,
        role: column?.role ? formLayoutRoleNames[column.role] : null,
        label: column?.label ?? null,
        missing: change.now === columnNoLongerPresent,
      }
    })
}
