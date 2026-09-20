import type { SmtpSettings } from './api'
import type { SmtpPresetId } from './validation'

/**
 * The effective-source status line (decision 10): one muted sentence stating what
 * actually sends mail. Updates in place after save/remove — the success feedback.
 */
export function smtpStatusLine(settings: SmtpSettings): string {
  if (settings.source === 'none') {
    return 'Email sending is not configured.'
  }
  const via = `Sending as ${settings.fromAddress} via ${settings.host}:${settings.port}`
  return settings.source === 'settings'
    ? `${via} — saved on this page.`
    : `${via} — from the configuration file.`
}

/** The preset matching an effective host; an unknown host is Custom, nothing defaults to Gmail. */
export function smtpPresetForHost(host: string | null): SmtpPresetId {
  if (host === 'smtp.gmail.com') {
    return 'gmail'
  }
  if (host === 'smtp.office365.com') {
    return 'outlook'
  }
  return host === null ? 'gmail' : 'custom'
}

/** The ProblemDetails detail text of an ApiError, when the server sent one. */
export function problemDetailText(error: unknown): string | null {
  if (typeof error !== 'object' || error === null || !('problem' in error)) {
    return null
  }
  const problem = (error as { problem?: unknown }).problem
  if (typeof problem !== 'object' || problem === null || !('detail' in problem)) {
    return null
  }
  const detail = (problem as { detail?: unknown }).detail
  return typeof detail === 'string' && detail.length > 0 ? detail : null
}
