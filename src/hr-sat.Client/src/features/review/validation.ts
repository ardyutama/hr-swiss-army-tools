/** Client-side input rules for the review flow; the server owns the business rules. */
export const notesMaxLength = 4000

export function notesValidationError(notes: string): string | null {
  if (notes.length > notesMaxLength) {
    return `Notes must be ${notesMaxLength} characters or fewer.`
  }
  return null
}
