import { shallowRef, toValue, type MaybeRefOrGetter } from 'vue'
import { listTemplateSources, type EmailTemplateKind, type TemplateSource } from './api'

/**
 * Copy-from sources per template kind: every other vacancy owning that kind,
 * closed included, newest first. Best-effort like the vacancy edit prefill —
 * when the fetch fails the picker hides itself (empty list) rather than
 * blocking template management. The latest call per kind wins, so a stale
 * response never overwrites a fresh one.
 */
export function useTemplateSources(vacancyId: MaybeRefOrGetter<string>) {
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

  return { sources, loadSources }
}
