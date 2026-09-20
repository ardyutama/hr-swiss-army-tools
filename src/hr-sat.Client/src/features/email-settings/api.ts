import { delJson, getJson, postJson, putJson } from '@/shared/http'

/** Where the effective SMTP Account comes from (server contract, issue 02 decision 12). */
export type SmtpSettingsSource = 'settings' | 'configuration-file' | 'none'

/**
 * The effective SMTP Account as the server reports it. The password is write-only and
 * never returned — `hasPassword` flags a stored one. `source: 'none'` means every field
 * is null and the installation cannot send.
 */
export interface SmtpSettings {
  host: string | null
  port: number | null
  username: string | null
  fromAddress: string | null
  fromName: string | null
  hasPassword: boolean
  source: SmtpSettingsSource
}

/** The saved values; a null password keeps the stored one (decision 12). */
export interface SmtpSettingsWritePayload {
  host: string
  port: number
  username: string
  password: string | null
  fromAddress: string
  fromName: string | null
}

/** The unsaved values to probe; a null password falls back to the stored one (decision 3). */
export interface SmtpTestPayload {
  host: string
  port: number
  username: string
  password: string | null
}

/** The passing probe's server-owned copy ("Connected and authenticated."). */
export interface SmtpTestPassed {
  message: string
}

export function getSmtpSettings(): Promise<SmtpSettings> {
  return getJson<SmtpSettings>('/api/settings/smtp')
}

export function upsertSmtpSettings(payload: SmtpSettingsWritePayload): Promise<SmtpSettings> {
  return putJson<SmtpSettings>('/api/settings/smtp', payload)
}

export function removeSmtpSettings(): Promise<void> {
  return delJson('/api/settings/smtp')
}

export function testSmtpConnection(payload: SmtpTestPayload): Promise<SmtpTestPassed> {
  return postJson<SmtpTestPassed>('/api/settings/smtp/test', payload)
}
