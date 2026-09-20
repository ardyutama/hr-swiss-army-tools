import { computed, shallowRef, toValue, type MaybeRefOrGetter } from 'vue'
import { useToast } from '@nuxt/ui/composables/useToast'
import { problemMessage, problemMessageText } from '@/shared/problem-details'
import {
  deleteEmailTemplate,
  listEmailTemplates,
  listTemplateSources,
  upsertEmailTemplate,
  type EmailTemplate,
  type EmailTemplateKind,
  type EmailTemplateWritePayload,
  type TemplateSource,
  type VacancyEmailTemplates,
} from './api'
import { emailTemplateKindLabel } from './format'

export type EmailTemplatesViewState = 'loading' | 'error' | 'ready'

function templatesByKind(items: EmailTemplate[]): VacancyEmailTemplates {
  return {
    shortlisted: items.find((template) => template.kind === 'shortlisted') ?? null,
    rejected: items.find((template) => template.kind === 'rejected') ?? null,
  }
}

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

  // Copy-from sources per template kind: every other vacancy owning that kind,
  // closed included, newest first. Best-effort like the vacancy edit prefill —
  // when the fetch fails the picker hides itself (empty list) rather than
  // blocking template management. The latest call per kind wins, so a stale
  // response never overwrites a fresh one.
  const sources = shallowRef<Partial<Record<EmailTemplateKind, TemplateSource[]>>>({})
  const tokens: Record<EmailTemplateKind, number> = { shortlisted: 0, rejected: 0 }

  function loadSources(kind: EmailTemplateKind) {
    const requestToken = ++tokens[kind]
    listTemplateSources(toValue(vacancyId), kind)
      .then((list) => {
        if (tokens[kind] === requestToken) {
          sources.value =
            kind === 'shortlisted'
              ? { ...sources.value, shortlisted: list }
              : { ...sources.value, rejected: list }
        }
      })
      .catch(() => {
        if (tokens[kind] === requestToken) {
          sources.value =
            kind === 'shortlisted'
              ? { ...sources.value, shortlisted: [] }
              : { ...sources.value, rejected: [] }
        }
      })
  }

  return {
    templates,
    loadError,
    viewState,
    saving,
    deleting,
    load,
    save,
    remove,
    sources,
    loadSources,
  }
}
