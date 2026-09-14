import { computed, shallowRef, toValue, type MaybeRefOrGetter } from 'vue'
import { useToast } from '@nuxt/ui/composables/useToast'
import { problemMessage, problemMessageText } from '@/shared/problem-details'
import {
  deleteEmailTemplate,
  listEmailTemplates,
  upsertEmailTemplate,
  type EmailTemplate,
  type EmailTemplateKind,
  type EmailTemplateWritePayload,
  type VacancyEmailTemplates,
} from './api'

export type EmailTemplatesViewState = 'loading' | 'error' | 'ready'

export function emailTemplateKindLabel(kind: EmailTemplateKind): string {
  return kind === 'shortlisted' ? 'Shortlisted' : 'Rejected'
}

function templatesByKind(items: EmailTemplate[]): VacancyEmailTemplates {
  return {
    shortlisted: items.find((template) => template.kind === 'shortlisted') ?? null,
    rejected: items.find((template) => template.kind === 'rejected') ?? null,
  }
}

/**
 * Owns the two per-vacancy templates inside the email-templates dialog. Success
 * toasts live here; failures are re-thrown without a toast so the dialog can
 * surface them inline (ADR-0008 #14: errors render inline, never toast-only).
 */
export function useEmailTemplates(vacancyId: MaybeRefOrGetter<string>) {
  const toast = useToast()
  const templates = shallowRef<VacancyEmailTemplates | null>(null)
  const loadError = shallowRef<string | null>(null)
  const saving = shallowRef(false)
  const deleting = shallowRef(false)

  const viewState = computed<EmailTemplatesViewState>(() => {
    if (loadError.value !== null) {
      return 'error'
    }
    return templates.value === null ? 'loading' : 'ready'
  })

  async function load() {
    loadError.value = null
    try {
      templates.value = templatesByKind(await listEmailTemplates(toValue(vacancyId)))
    } catch (error) {
      loadError.value = problemMessageText(problemMessage(error, "Couldn't load email templates"))
    }
  }

  /** Upserts one template, then patches state from the returned DTO so it cannot drift. */
  async function save(kind: EmailTemplateKind, payload: EmailTemplateWritePayload): Promise<void> {
    saving.value = true
    try {
      const saved = await upsertEmailTemplate(toValue(vacancyId), kind, payload)
      const current = templates.value ?? { shortlisted: null, rejected: null }
      templates.value =
        kind === 'shortlisted'
          ? { ...current, shortlisted: saved }
          : { ...current, rejected: saved }
      toast.add({ title: `${emailTemplateKindLabel(kind)} template saved`, color: 'success' })
    } finally {
      saving.value = false
    }
  }

  /** Deletes one template. Failures re-throw so the section keeps the failure visible inline. */
  async function remove(kind: EmailTemplateKind): Promise<void> {
    deleting.value = true
    try {
      await deleteEmailTemplate(toValue(vacancyId), kind)
      const current = templates.value
      if (current) {
        templates.value =
          kind === 'shortlisted'
            ? { ...current, shortlisted: null }
            : { ...current, rejected: null }
      }
      toast.add({ title: `${emailTemplateKindLabel(kind)} template deleted`, color: 'success' })
    } finally {
      deleting.value = false
    }
  }

  return { templates, loadError, viewState, saving, deleting, load, save, remove }
}
