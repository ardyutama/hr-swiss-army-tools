import { describe, expect, it } from 'vitest'

import { firstNonFieldError, formErrorsOnly, type FieldErrors } from './validation'

describe('shared validation helpers', () => {
  const fieldKeys = new Set(['title', 'openedon'])

  it('keeps only errors for the supplied form fields', () => {
    const errors: FieldErrors = {
      title: ['Title is invalid.'],
      conflict: ['The vacancy was changed by someone else.'],
    }

    expect(formErrorsOnly(errors, fieldKeys)).toEqual({
      title: ['Title is invalid.'],
    })
  })

  it('finds the first non-empty error outside the form fields', () => {
    const errors: FieldErrors = {
      title: ['Title is invalid.'],
      summary: [''],
      conflict: ['The vacancy was changed by someone else.'],
    }

    expect(firstNonFieldError(errors, fieldKeys)).toBe(
      'The vacancy was changed by someone else.',
    )
  })
})