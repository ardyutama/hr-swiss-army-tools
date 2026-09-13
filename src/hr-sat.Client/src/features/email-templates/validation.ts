import { z } from 'zod'

export const SUBJECT_MAX_LENGTH = 998

/** The two placeholders a template may carry (glossary: Email Template). */
export const templatePlaceholders = [
  { token: '{{candidate_name}}', label: 'Candidate name' },
  { token: '{{vacancy_title}}', label: 'Vacancy title' },
] as const

/**
 * Request-shape rules only — the DDL allows an empty subject, but an empty
 * subject would produce broken mailtos once ticket 07 sends, so the form
 * requires one. The server stays the authority on business rules.
 */
export const templateFormSchema = z.object({
  subject: z
    .string()
    .trim()
    .min(1, 'Subject is required.')
    .max(SUBJECT_MAX_LENGTH, `Subject must be ${SUBJECT_MAX_LENGTH} characters or fewer.`),
  body: z.string().trim().min(1, 'Body is required.'),
})

export type TemplateFormOutput = z.output<typeof templateFormSchema>

/** Server error keys arrive lowercased; map them onto the schema's field names. */
export const templateServerFieldNames: Record<string, string> = {
  subject: 'subject',
  body: 'body',
}

/** Keys of the form fields as the server reports them (lowercased). */
export const templateFormFieldKeys: ReadonlySet<string> = new Set(
  Object.keys(templateServerFieldNames),
)
