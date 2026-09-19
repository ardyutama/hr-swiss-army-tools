import { z } from 'zod'
import type { FormLayoutColumn, FormLayoutRole } from './api'

export const maxPickedColumns = 8
export const maxColumnLabelLength = 40

/**
 * One editable row per pickable form column. Ordinal 0 (the Google Forms
 * Timestamp) is system data and never becomes a row. `present` is false for a
 * picked column whose ordinal no longer exists in the file being mapped — the
 * row stays listed so HR can un-pick it during a drift re-map.
 */
export interface FormLayoutRow {
  ordinal: number
  header: string
  role: FormLayoutRole | null
  display: boolean
  label: string
  present: boolean
}

export interface FormLayoutFormState {
  rows: FormLayoutRow[]
}

const roleSchema = z.enum(['name', 'contactEmail', 'contactPhone', 'cvLink'])

const rowSchema = z.object({
  ordinal: z.number().int().min(1),
  header: z.string(),
  role: roleSchema.nullable(),
  display: z.boolean(),
  present: z.boolean(),
  label: z
    .string()
    .max(
      maxColumnLabelLength,
      `Column labels must be ${maxColumnLabelLength} characters or fewer.`,
    )
    .refine((value) => value === value.trim(), {
      message: 'Column labels must be trimmed.',
    })
    .refine((value) => !value.includes('\n') && !value.includes('\r'), {
      message: 'Column labels must be single line.',
    }),
})

/**
 * Input rules the panel enforces before the server is asked: Name and Contact
 * email bound, at most 8 picked columns (roles included), label format, and no
 * picked column that the file no longer has. Business invariants stay
 * server-owned; these mirror them for feedback speed.
 */
export function createFormLayoutSchema(headerCount: number) {
  return z.object({
    rows: z.array(rowSchema).superRefine((rows, ctx) => {
      if (
        !rows.some((row) => row.role === 'name') ||
        !rows.some((row) => row.role === 'contactEmail')
      ) {
        ctx.addIssue({
          code: 'custom',
          message: 'Name and Contact email are required.',
          path: ['rows'],
        })
      }

      const picked = rows.filter((row) => row.role !== null || row.display)
      if (picked.length > maxPickedColumns) {
        ctx.addIssue({
          code: 'custom',
          message: `${maxPickedColumns} columns at most, roles included.`,
          path: ['rows'],
        })
      }

      for (const row of picked) {
        if (!row.present || row.ordinal >= headerCount) {
          ctx.addIssue({
            code: 'custom',
            message: `Column ${row.ordinal} is no longer in the file — pick another column or un-pick it.`,
            path: ['rows'],
          })
        }
      }
    }),
  })
}

export type FormLayoutFormOutput = z.output<ReturnType<typeof createFormLayoutSchema>>

/**
 * Builds the editor rows: one per pickable header, pre-filled from the current
 * mapping. Picked columns the file no longer contains keep their row (marked
 * not-present) so a re-map can adjust or un-pick them; `fallbackHeaders` is the
 * saved snapshot used to name those columns.
 */
export function formLayoutRows(
  headers: readonly string[],
  initialColumns: readonly FormLayoutColumn[] | null,
  fallbackHeaders?: readonly string[],
): FormLayoutRow[] {
  const initialByOrdinal = new Map(
    (initialColumns ?? []).map((column) => [column.ordinal, column]),
  )

  const rows: FormLayoutRow[] = []
  for (let ordinal = 1; ordinal < headers.length; ordinal++) {
    const initial = initialByOrdinal.get(ordinal)
    initialByOrdinal.delete(ordinal)
    rows.push({
      ordinal,
      header: headers[ordinal] ?? '',
      role: initial?.role ?? null,
      display: initial != null && initial.role === null,
      label: initial?.label ?? '',
      present: true,
    })
  }

  const missing = [...initialByOrdinal.values()]
    .sort((left, right) => left.ordinal - right.ordinal)
    .map((column) => ({
      ordinal: column.ordinal,
      header: fallbackHeaders?.[column.ordinal] ?? '',
      role: column.role,
      display: column.role === null,
      label: column.label ?? '',
      present: false,
    }))

  return [...rows, ...missing]
}

/** Projects the editor rows back to the picked-column list the API expects. */
export function formLayoutColumnsFromRows(rows: readonly FormLayoutRow[]): FormLayoutColumn[] {
  return rows
    .filter((row) => row.role !== null || row.display)
    .map((row) => {
      const label = row.label.trim()
      return { ordinal: row.ordinal, role: row.role, label: label === '' ? null : label }
    })
    .sort((left, right) => left.ordinal - right.ordinal)
}
