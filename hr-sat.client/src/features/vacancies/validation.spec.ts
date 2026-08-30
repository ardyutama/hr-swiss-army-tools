import { describe, expect, it } from 'vitest'

import {
  vacancyRequirementValues,
  validateVacancyForm,
  type VacancyFormValues,
} from './validation'

describe('vacancy form validation', () => {
  it('reports the required vacancy fields', () => {
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

  it('trims and removes blank requirements for the API payload', () => {
    expect(
      vacancyRequirementValues([' MIG welding ', ' ', 'Blueprint reading']),
    ).toEqual(['MIG welding', 'Blueprint reading'])
  })

  it('accepts a complete vacancy form', () => {
    expect(
      validateVacancyForm({
        title: 'Senior Welder',
        openedOn: '2026-08-30',
        requirements: ['MIG welding'],
      }),
    ).toEqual({})
  })
})