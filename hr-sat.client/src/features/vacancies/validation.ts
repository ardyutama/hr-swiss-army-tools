import type { FieldErrors } from '@/shared/validation'

export interface VacancyFormValues {
  title: string
  openedOn: string
  requirements: readonly string[]
}

export const vacancyFormFieldKeys: ReadonlySet<string> = new Set([
  'title',
  'openedon',
  'requirements',
])

export function vacancyRequirementValues(requirements: readonly string[]): string[] {
  return requirements.map((requirement) => requirement.trim()).filter((requirement) => requirement.length > 0)
}

export function validateVacancyForm(values: VacancyFormValues): FieldErrors {
  const errors: FieldErrors = {}
  if (values.title.trim().length === 0) {
    errors.title = ['Title is required.']
  } else if (values.title.trim().length > 200) {
    errors.title = ['Title must be 200 characters or fewer.']
  }
  if (!values.openedOn) {
    errors.openedon = ['Opening date is required.']
  }
  if (vacancyRequirementValues(values.requirements).length === 0) {
    errors.requirements = ['Add at least one requirement.']
  }
  return errors
}