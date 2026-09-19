import type { FormLayoutRole } from './api'
import { maxPickedColumns } from './validation'

/** Display names for the four bindable roles. */
export const formLayoutRoleNames: Record<FormLayoutRole, string> = {
  name: 'Name',
  contactEmail: 'Contact email',
  contactPhone: 'Contact phone',
  cvLink: 'CV link',
}

const maxHeaderLength = 48

export function truncateHeader(header: string, max: number = maxHeaderLength): string {
  const collapsed = header.replace(/\s+/g, ' ').trim()
  return collapsed.length > max ? `${collapsed.slice(0, max - 1)}…` : collapsed
}

/** The "2 · Nama Lengkap" reference used in role selects, checkboxes, and the summary. */
export function columnReference(ordinal: number, header: string): string {
  return `${ordinal} · ${truncateHeader(header)}`
}

/**
 * Live counter over the display-field list: below the cap it reads
 * "Picked 4 / 8"; at the cap it becomes the explanation itself.
 */
export function pickedCounterText(picked: number, max: number = maxPickedColumns): string {
  return picked >= max ? `${max} columns at most, roles included` : `Picked ${picked} / ${max}`
}
