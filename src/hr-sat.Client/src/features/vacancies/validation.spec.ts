import { describe, expect, it } from 'vitest'

import {
  vacancyFormSchema,
  vacancyRequirementValues,
} from './validation'

function issuePaths(values: unknown): string[] {
  const result = vacancyFormSchema.safeParse(values)
  if (result.success) {
    return []
  }
  return result.error.issues.map((issue) => `${issue.path.join('.')}: ${issue.message}`)
}

describe('vacancy form schema', () => {
  it('US-9: rejects a form missing the required title, opening date, and requirements', () => {
    const issues = issuePaths({
      title: '  ',
      openedOn: '',
      requirements: [{ id: 0, value: '  ' }],
    })

    expect(issues).toContain('title: Title is required.')
    expect(issues).toContain('openedOn: Opening date is required.')
    expect(issues).toContain('requirements: Add at least one requirement.')
  })

  it('US-9: accepts a complete vacancy form', () => {
    const result = vacancyFormSchema.safeParse({
      title: 'Senior Welder',
      openedOn: '2026-08-30',
      requirements: [{ id: 0, value: 'MIG welding' }],
    })

    expect(result.success).toBe(true)
  })

  it('domain: vacancy requirement is ordered and non-empty — trims blanks and preserves order for the payload', () => {
    expect(
      vacancyRequirementValues([' MIG welding ', ' ', 'Blueprint reading']),
    ).toEqual(['MIG welding', 'Blueprint reading'])
  })
})