import {
  computed,
  onScopeDispose,
  shallowRef,
  toValue,
  watch,
  type MaybeRefOrGetter,
} from 'vue'
import type { CandidateSummary } from '@/features/candidates/api'
import { contactability, contactabilityPlan } from '@/features/candidates/format'
import type {
  EmailTemplate,
  EmailTemplateKind,
  RenderedMessage,
} from '@/features/email-templates/api'
import { listEmailTemplates, renderEmailTemplate } from '@/features/email-templates/api'
import { problemMessage, problemMessageText } from '@/shared/problem-details'
import { clipboardText } from './format'

/** One Prepared Message row's render lifecycle. */
export type PreparedMessageState =
  | { kind: 'loading' }
  | { kind: 'error'; message: string }
  | { kind: 'ready'; message: RenderedMessage }

export interface PreparedMessageRow {
  candidate: CandidateSummary
  state: PreparedMessageState
}

export type PreparedMessagesViewState = 'loading' | 'error' | 'ready'

const COPIED_FEEDBACK_MS = 2000

/**
 * Owns the Prepared Message list behind the send dialog. On open it fetches
 * the vacancy's Email Templates, then renders one message per Contactable
 * Candidate through parallel `POST /render` calls — no bulk endpoint and no
 * client-side placeholder duplication. Each row carries its own
 * loading/error/ready union with a per-row retry; a `templatesRevision` bump
 * (the stacked templates dialog closed) refetches and re-renders everything.
 * Nothing here records a message as sent — the system never sends.
 */
export function usePreparedMessages(
  vacancyId: MaybeRefOrGetter<string>,
  candidates: MaybeRefOrGetter<CandidateSummary[]>,
  open: MaybeRefOrGetter<boolean>,
  templatesRevision: MaybeRefOrGetter<number>,
) {
  const templates = shallowRef<EmailTemplate[] | null>(null)
  const loadError = shallowRef<string | null>(null)
  const rows = shallowRef<PreparedMessageRow[]>([])
  const copiedCandidateId = shallowRef<number | null>(null)
  let copyTimer: ReturnType<typeof setTimeout> | null = null
  let generation = 0

  const viewState = computed<PreparedMessagesViewState>(() => {
    if (loadError.value !== null) {
      return 'error'
    }
    return templates.value === null ? 'loading' : 'ready'
  })

  const plan = computed(() => contactabilityPlan(toValue(candidates)))
  const contactable = computed(() => plan.value.contactable)
  const missingEmail = computed(() => plan.value.missingEmail)
  const excludedUndecided = computed(() => plan.value.undecided.length)
  const excludedOutcome = computed(() => plan.value.outcomeRecorded.length)

  /** Kinds with at least one Contactable Candidate but no template — blocked with a path forward. */
  const missingTemplateKinds = computed<EmailTemplateKind[]>(() => {
    const present = templates.value ?? []
    const kinds = new Set<EmailTemplateKind>()
    for (const candidate of contactable.value) {
      const classification = contactability(candidate)
      if (
        classification.kind === 'contactable' &&
        !present.some((template) => template.kind === classification.decidedAs)
      ) {
        kinds.add(classification.decidedAs)
      }
    }
    return [...kinds]
  })

  function setRowState(candidateId: number, state: PreparedMessageState, token: number) {
    if (token !== generation) {
      return
    }
    rows.value = rows.value.map((row) =>
      row.candidate.id === candidateId ? { ...row, state } : row,
    )
  }

  async function renderRow(row: PreparedMessageRow, token: number) {
    if (token !== generation) {
      return
    }
    const classification = contactability(row.candidate)
    if (classification.kind !== 'contactable') {
      return
    }
    const template = (templates.value ?? []).find(
      (item) => item.kind === classification.decidedAs,
    )
    if (!template) {
      return
    }
    setRowState(row.candidate.id, { kind: 'loading' }, token)
    try {
      const message = await renderEmailTemplate(toValue(vacancyId), {
        subject: template.subject,
        body: template.body,
        candidateId: row.candidate.id,
      })
      setRowState(row.candidate.id, { kind: 'ready', message }, token)
    } catch (error) {
      setRowState(row.candidate.id, {
        kind: 'error',
        message: problemMessageText(problemMessage(error, "Couldn't prepare this message")),
      }, token)
    }
  }

  async function load() {
    const token = ++generation
    loadError.value = null
    templates.value = null
    rows.value = []
    copiedCandidateId.value = null
    try {
      const list = await listEmailTemplates(toValue(vacancyId))
      if (token !== generation) {
        return
      }
      templates.value = list
      if (token !== generation) {
        return
      }
      rows.value = contactable.value.flatMap<PreparedMessageRow>((candidate) => {
        const classification = contactability(candidate)
        if (
          classification.kind !== 'contactable' ||
          !list.some((template) => template.kind === classification.decidedAs)
        ) {
          return []
        }
        return [{ candidate, state: { kind: 'loading' } }]
      })
      // The renders fan out in parallel; each row resolves on its own.
      await Promise.all(rows.value.map((row) => renderRow(row, token)))
    } catch (error) {
      if (token !== generation) {
        return
      }
      loadError.value = problemMessageText(problemMessage(error, "Couldn't load email templates"))
    }
  }

  /** Re-renders one failed row, leaving the rest of the list alone. */
  function retry(candidate: CandidateSummary) {
    const row = rows.value.find((item) => item.candidate.id === candidate.id)
    if (row) {
      void renderRow(row, generation)
    }
  }

  /** Clipboard fallback: writes To/Subject/body and flags the row as copied — no toast. */
  async function copy(candidate: CandidateSummary, message: RenderedMessage) {
    await navigator.clipboard.writeText(
      clipboardText(candidate.contactEmail ?? '', message.subject, message.body),
    )
    copiedCandidateId.value = candidate.id
    if (copyTimer !== null) {
      clearTimeout(copyTimer)
    }
    copyTimer = setTimeout(() => {
      copiedCandidateId.value = null
      copyTimer = null
    }, COPIED_FEEDBACK_MS)
  }

  watch(
    () => toValue(open),
    (isOpen) => {
      if (isOpen) {
        void load()
      }
    },
  )

  watch(
    () => toValue(templatesRevision),
    () => {
      if (toValue(open)) {
        void load()
      }
    },
  )

  onScopeDispose(() => {
    if (copyTimer !== null) {
      clearTimeout(copyTimer)
    }
  })

  return {
    viewState,
    loadError,
    rows,
    contactableCount: computed(() => contactable.value.length),
    missingEmail,
    excludedUndecided,
    excludedOutcome,
    missingTemplateKinds,
    copiedCandidateId,
    load,
    retry,
    copy,
  }
}
