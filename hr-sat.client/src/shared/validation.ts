import type { ApiError } from './http'

/**
 * Field-level validation errors returned by the API as an ASP.NET
 * ValidationProblem (`errors: { [field]: string[] }`).
 */
export type FieldErrors = Record<string, string[]>

const validationCache = new WeakMap<ApiError, FieldErrors | null>()

/**
 * Extracts field errors from an ApiError produced by a 400 ValidationProblem.
 * Returns null when the error is not a validation problem.
 */
export function fieldErrorsOf(error: unknown): FieldErrors | null {
  if (!isApiError(error)) {
    return null
  }
  if (validationCache.has(error)) {
    return validationCache.get(error) ?? null
  }
  const errors = parseValidationProblem(error.problem)
  validationCache.set(error, errors)
  return errors
}

function isApiError(error: unknown): error is ApiError {
  return (
    typeof error === 'object' &&
    error !== null &&
    'status' in error &&
    'problem' in error
  )
}

function parseValidationProblem(problem: unknown): FieldErrors | null {
  if (typeof problem !== 'object' || problem === null || !('errors' in problem)) {
    return null
  }
  const raw = (problem as { errors?: unknown }).errors
  if (typeof raw !== 'object' || raw === null) {
    return null
  }
  const result: FieldErrors = {}
  for (const [key, value] of Object.entries(raw)) {
    result[key.toLowerCase()] = Array.isArray(value) ? value.map(String) : [String(value)]
  }
  return result
}
