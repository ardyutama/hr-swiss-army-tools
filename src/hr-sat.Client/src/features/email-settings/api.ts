import { delJson, getJson, postJson, putJson } from '@/shared/http'

/** Where the effective SMTP Account comes from (server contract, issue 02 decision 12). */
export type SmtpSettingsSource = 'settings' | 'configuration-file' | 'none'

/** How the SMTP Account authenticates (server contract, issue 03 decision 16). */
export type SignInMethod = 'app-password' | 'microsoft-account'

/**
 * The effective SMTP Account as the server reports it. The password is write-only and
 * never returned — `hasPassword` flags a stored one. `source: 'none'` means every field
 * is null and the installation cannot send. `signInMethod` is null only for `none`;
 * the configuration file is always `app-password` (decision 11).
 * `microsoftSignInAvailable` disables the Microsoft choice before the click (decision 24).
 */
export interface SmtpSettings {
  host: string | null
  port: number | null
  username: string | null
  fromAddress: string | null
  fromName: string | null
  hasPassword: boolean
  signInMethod: SignInMethod | null
  microsoftSignInAvailable: boolean
  source: SmtpSettingsSource
}

/**
 * The saved values, discriminated by Sign-in Method (issue 03, decision 11): under
 * `app-password` the full credential set (a null password keeps the stored one);
 * under `microsoft-account` only the From fields — the server derives the credential
 * columns from the connected account.
 */
export type SmtpSettingsWritePayload =
  | {
      signInMethod: 'app-password'
      host: string
      port: number
      username: string
      password: string | null
      fromAddress: string
      fromName: string | null
    }
  | {
      signInMethod: 'microsoft-account'
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

/** The device-code challenge the connect panel shows (server contract, decision 32). */
export interface MicrosoftConnectChallenge {
  userCode: string
  verificationUrl: string
  expiresAt: string
}

/** The connect attempt states the wire carries (server contract, decision 21). */
export type MicrosoftConnectStateValue = 'idle' | 'pending' | 'succeeded' | 'failed' | 'expired'

/** The connect attempt's outcome as the status poll reports it (decision 32). */
export interface MicrosoftConnectStatus {
  state: MicrosoftConnectStateValue
  accountEmail?: string | null
  error?: string | null
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

export function beginMicrosoftConnect(): Promise<MicrosoftConnectChallenge> {
  return postJson<MicrosoftConnectChallenge>('/api/settings/smtp/microsoft-connect/begin', {})
}

export function getMicrosoftConnectStatus(): Promise<MicrosoftConnectStatus> {
  return getJson<MicrosoftConnectStatus>('/api/settings/smtp/microsoft-connect/status')
}
