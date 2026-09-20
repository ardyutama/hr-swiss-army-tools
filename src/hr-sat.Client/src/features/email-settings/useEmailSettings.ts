import { computed, shallowRef } from 'vue'
import { problemMessage, problemMessageText } from '@/shared/problem-details'
import {
  getSmtpSettings,
  removeSmtpSettings,
  testSmtpConnection,
  upsertSmtpSettings,
  type SmtpSettings,
  type SmtpSettingsWritePayload,
  type SmtpTestPassed,
  type SmtpTestPayload,
} from './api'

export type EmailSettingsViewState = 'loading' | 'error' | 'ready'

/**
 * The email-settings flow's async lifecycles. Errors from save/test/remove propagate
 * to the view, which routes them onto the form — mutations never touch the loaded
 * settings on failure.
 */
export function useEmailSettings() {
  const settings = shallowRef<SmtpSettings | null>(null)
  const loadError = shallowRef<string | null>(null)
  const saving = shallowRef(false)
  const testing = shallowRef(false)
  const removing = shallowRef(false)

  const viewState = computed<EmailSettingsViewState>(() => {
    if (loadError.value !== null) {
      return 'error'
    }
    return settings.value === null ? 'loading' : 'ready'
  })

  async function load() {
    loadError.value = null
    try {
      settings.value = await getSmtpSettings()
    } catch (error) {
      loadError.value = problemMessageText(problemMessage(error, "Couldn't load the email settings"))
    }
  }

  // Pessimistic: the PUT answers with the fresh effective settings, which replace the
  // loaded ones — the status line updating in place is the success feedback
  // (decision 10; no toast).
  async function save(payload: SmtpSettingsWritePayload): Promise<void> {
    saving.value = true
    try {
      settings.value = await upsertSmtpSettings(payload)
    } finally {
      saving.value = false
    }
  }

  // The probe tests the values as typed and persists nothing (decision 3).
  async function testConnection(payload: SmtpTestPayload): Promise<SmtpTestPassed> {
    testing.value = true
    try {
      return await testSmtpConnection(payload)
    } finally {
      testing.value = false
    }
  }

  // After removal the configuration file (if any) resumes — reload so the status
  // line and the form prefill reflect the new effective source (decision 11).
  async function removeSettings(): Promise<void> {
    removing.value = true
    try {
      await removeSmtpSettings()
      await load()
    } finally {
      removing.value = false
    }
  }

  return {
    settings,
    loadError,
    viewState,
    saving,
    testing,
    removing,
    load,
    save,
    testConnection,
    removeSettings,
  }
}
