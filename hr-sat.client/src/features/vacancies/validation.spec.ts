import { describe, expect, it } from 'vitest'

import {
  vacancyRequirementValues,
  validateVacancyForm,
  type VacancyFormValues,
} from './validation'

describe('vacancy form validation', () => {
  it('US-9: rejects a form missing the required title, opening date, and requirements', () => {
    const values: VacancyFormValues = {
      title: '  ',
      openedOn: '',
      requirements: ['  '],
    }

    expect(validateVacancyForm(values)).toEqual({
      title: ['Title is required.'],
      openedon: ['Opening date is required.'],
      requirements: ['Add at least one requirement.'],
    })
  })

  it('US-9: accepts a complete vacancy form', () => {
    expect(
      validateVacancyForm({
        title: 'Senior Welder',
        openedOn: '2026-08-30',
        requirements: ['MIG welding'],
      }),
    ).toEqual({})
  })

  it('domain: vacancy requirement is ordered and non-empty — trims blanks and preserves order for the payload', () => {
    expect(
      vacancyRequirementValues([' MIG welding ', ' ', 'Blueprint reading']),
    ).toEqual(['MIG welding', 'Blueprint reading'])
  })
})