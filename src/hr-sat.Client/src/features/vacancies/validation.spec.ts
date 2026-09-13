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

  it('US-9: accepts an unset hiring target and both inclusive bounds', () => {
    for (const neededHires of [null, 1, 9999]) {
      const result = vacancyFormSchema.safeParse({
        title: 'Senior Welder',
        openedOn: '2026-08-30',
        neededHires,
        requirements: [{ id: 0, value: 'MIG welding' }],
      })

      expect(result.success).toBe(true)
    }
  })

  it('US-9: rejects hiring targets outside the 1-9999 range', () => {
    for (const neededHires of [0, 10000]) {
      const issues = issuePaths({
        title: 'Senior Welder',
        openedOn: '2026-08-30',
        neededHires,
        requirements: [{ id: 0, value: 'MIG welding' }],
      })

      expect(issues.some((issue) => issue.startsWith('neededHires:'))).toBe(true)
    }
  })

  it('domain: vacancy requirement is ordered and non-empty — trims blanks and preserves order for the payload', () => {
    expect(
      vacancyRequirementValues([' MIG welding ', ' ', 'Blueprint reading']),
    ).toEqual(['MIG welding', 'Blueprint reading'])
  })
})