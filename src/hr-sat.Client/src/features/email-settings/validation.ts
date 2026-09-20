import { z } from 'zod'

export const FROM_NAME_MAX_LENGTH = 200

export type SmtpPresetId = 'gmail' | 'outlook' | 'custom'

export interface SmtpPreset {
  id: SmtpPresetId
  label: string
  /** Empty for Custom — it leaves host and port blank (decision 5). */
  host: string
  port: number | null
  /** Helper line shown under the selector while the preset is selected; '' hides it. */
  help: string
}

/**
 * The three presets (decision 5). No "Office 365 legacy basic auth" preset — Microsoft
 * retired SMTP basic auth, so that path would ship a dead end; Custom covers it.
 */
export const smtpPresets: SmtpPreset[] = [
  {
    id: 'gmail',
    label: 'Gmail',
    host: 'smtp.gmail.com',
    port: 587,
    help: 'Requires 2FA → create an app password',
  },
  {
    id: 'outlook',
    label: 'Outlook',
    host: 'smtp.office365.com',
    port: 587,
    help: 'Create an app password in account security',
  },
  { id: 'custom', label: 'Custom', host: '', port: null, help: '' },
]

/** Bare hostname or IP, no scheme, no slashes (decision 6) — the server owns the strict check. */
function isHostLike(value: string): boolean {
  return value.length > 0 && !value.includes('://') && !value.includes('/') && !/\s/.test(value)
}

const hostSchema = z
  .string()
  .trim()
  .min(1, 'Enter a hostname or IP address.')
  .refine(isHostLike, 'Enter a hostname or IP address.')

const portSchema = z
  .string()
  .trim()
  .min(1, 'Enter a port between 1 and 65535.')
  .regex(/^\d+$/, 'Enter a port between 1 and 65535.')
  .transform(Number)
  .pipe(z.number().int().min(1, 'Enter a port between 1 and 65535.').max(65535, 'Enter a port between 1 and 65535.'))

export interface SmtpFormContext {
  preset: SmtpPresetId
  /** True when a saved row holds a password — the field may stay untouched (decision 6). */
  hasStoredPassword: boolean
}

/**
 * Input rules for the settings form; the server mirrors them and owns business rules.
 * The strict username email shape applies only under the Gmail/Outlook presets
 * (decision 6); Custom usernames are free text.
 */
export function smtpFormSchema(context: SmtpFormContext) {
  return z.object({
    host: hostSchema,
    port: portSchema,
    username:
      context.preset === 'custom'
        ? z.string().trim().min(1, 'Enter the account username.')
        : z
            .string()
            .trim()
            .min(1, 'Enter the account username.')
            .pipe(z.email('Enter the full email address.')),
    password: context.hasStoredPassword
      ? z.string()
      : z.string().trim().min(1, 'Enter the app password.'),
    fromAddress: z
      .string()
      .trim()
      .min(1, 'Enter a valid email address.')
      .pipe(z.email('Enter a valid email address.')),
    fromName: z
      .string()
      .trim()
      .max(FROM_NAME_MAX_LENGTH, `From name must be ${FROM_NAME_MAX_LENGTH} characters or fewer.`),
  })
}

export type SmtpFormOutput = z.output<ReturnType<typeof smtpFormSchema>>

/** Server error keys arrive lowercased; map them onto the schema's field names. */
export const smtpServerFieldNames: Record<string, string> = {
  host: 'host',
  port: 'port',
  username: 'username',
  password: 'password',
  fromaddress: 'fromAddress',
  fromname: 'fromName',
}

/** Keys of the form fields as the server reports them (lowercased). */
export const smtpFormFieldKeys: ReadonlySet<string> = new Set(Object.keys(smtpServerFieldNames))
