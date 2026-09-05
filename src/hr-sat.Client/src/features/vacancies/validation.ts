import { z } from 'zod'

export interface VacancyRequirementRow {
  id: number
  value: string
}

export const vacancyFormSchema = z.object({
  title: z
    .string()
    .trim()
    .min(1, 'Title is required.')
    .max(200, 'Title must be 200 characters or fewer.'),
  openedOn: z.string().min(1, 'Opening date is required.'),
  requirements: z
    .array(z.object({ id: z.number(), value: z.string() }))
    .refine((rows) => rows.some((row) => row.value.trim().length > 0), {
      message: 'Add at least one requirement.',
    }),
})

export type VacancyFormOutput = z.output<typeof vacancyFormSchema>

/** Server error keys arrive lowercased; map them onto the schema's field names. */
export const vacancyServerFieldNames: Record<string, string> = {
  title: 'title',
  openedon: 'openedOn',
  requirements: 'requirements',
}

/** Keys of the form fields as the server reports them (lowercased). */
export const vacancyFormFieldKeys: ReadonlySet<string> = new Set(
  Object.keys(vacancyServerFieldNames),
)

export function vacancyRequirementValues(
  requirements: readonly string[],
): string[] {
  return requirements
    .map((requirement) => requirement.trim())
    .filter((requirement) => requirement.length > 0)
}
