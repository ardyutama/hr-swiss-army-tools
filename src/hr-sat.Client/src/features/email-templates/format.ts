import type { EmailTemplateKind } from './api'

/** Display label for an Email Template kind in section headings, alerts, and toasts. */
export function emailTemplateKindLabel(kind: EmailTemplateKind): string {
  return kind === 'shortlisted' ? 'Shortlisted' : 'Rejected'
}
