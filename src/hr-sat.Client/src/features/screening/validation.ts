import { z } from 'zod'
import type { ScreeningOperator, ScreeningRule } from './api'

export const maxScreeningRules = 5

const operatorSchema = z.enum(['equals', 'not-equals', 'is-empty', 'not-empty', 'contains'])

/** `equals` / `not-equals` / `contains` need a value; the empty operators forbid one. */
export function operatorNeedsValue(operator: ScreeningOperator): boolean {
  return operator === 'equals' || operator === 'not-equals' || operator === 'contains'
}

/**
 * Input rules mirrored from the server for feedback speed (the server owns the
 * invariants): at most 5 rules, ordinal ≥ 0, and the operator/value pairing.
 * Snapshot membership is deliberately NOT validated — an orphaned ordinal
 * stays keepable and removable, never a validation block (decision 32a).
 */
const ruleSchema = z
  .object({
    ordinal: z.number().int().min(0),
    operator: operatorSchema,
    value: z.string().nullable(),
  })
  .superRefine((rule, ctx) => {
    if (
      operatorNeedsValue(rule.operator) &&
      (rule.value === null || rule.value.trim() === '')
    ) {
      ctx.addIssue({
        code: 'custom',
        message: 'This operator needs a value.',
        path: ['value'],
      })
    }
    if (!operatorNeedsValue(rule.operator) && rule.value !== null) {
      ctx.addIssue({
        code: 'custom',
        message: 'This operator cannot have a value.',
        path: ['value'],
      })
    }
  })

export const screeningRulesFormSchema = z.object({
  rules: z.array(ruleSchema).max(maxScreeningRules, `${maxScreeningRules} rules at most.`),
})

export type ScreeningRulesFormOutput = z.output<typeof screeningRulesFormSchema>

/** A blank rule appended by "Add rule": first snapshot column, `equals`, empty value. */
export function blankScreeningRule(): ScreeningRule {
  return { ordinal: 0, operator: 'equals', value: '' }
}
