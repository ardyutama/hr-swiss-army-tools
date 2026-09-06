/** Client-side input rules for the review flow; the server owns the business rules. */
export const notesMaxLength = 4000
export const candidateNameMaxLength = 300
export const candidateEmailMaxLength = 320

export function notesValidationError(notes: string): string | null {
  if (notes.length > notesMaxLength) {
    return `Notes must be ${notesMaxLength} characters or fewer.`
  }
  return null
}

export interface CandidateDetailsValidationErrors {
  fullName?: string
  contactEmail?: string
}

export function candidateDetailsValidationErrors(
  fullName: string,
  contactEmail: string,
): CandidateDetailsValidationErrors {
  const errors: CandidateDetailsValidationErrors = {}
  if (fullName.trim().length === 0) {
    errors.fullName = 'Name is required.'
  } else if (fullName.trim().length > candidateNameMaxLength) {
    errors.fullName = `Name must be ${candidateNameMaxLength} characters or fewer.`
  }

  if (contactEmail.trim().length === 0) {
    errors.contactEmail = 'Email is required.'
  } else if (
    contactEmail.trim().length > candidateEmailMaxLength ||
    !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(contactEmail.trim())
  ) {
    errors.contactEmail = 'Enter a valid email address.'
  }

  return errors
}
