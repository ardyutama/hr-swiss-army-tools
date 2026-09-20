import { computed, shallowRef, watch, type Ref } from 'vue'
import type { CandidateSummary } from '@/features/candidates/api'
import { useMessagingSummary } from '@/features/candidates/useMessagingSummary'
import { useEmailTemplates } from '@/features/email-templates/useEmailTemplates'
import { usePreparedMessages } from '@/features/prepared-messages/usePreparedMessages'
import { sendScope } from '@/features/prepared-messages/format'
import { roundDisplayName } from '@/features/intake-rounds/useIntakeRounds'
import type { VacancyRound } from '@/features/vacancies/api'

export interface MessagingConsoleInput {
  vacancyId: Ref<string>
  /** The page's selectedRoundParam — '' while no round is selected. */
  roundId: Ref<string>
  /** The page's selected round — the send-scope naming derivation reads it. */
  selectedRound: Ref<VacancyRound | null>
}

/**
 * The vacancy-detail messaging console: composes the messaging summary (the
 * round's full unpaged candidate list), the Prepared Message list, and the
 * Email Templates flow, and owns what crosses between them — the send-scope
 * derivation (one candidate vs the whole round), the round naming, the two
 * dialog open flags, and the templates-revision signal: closing the templates
 * dialog bumps a revision the prepared-messages leaf watches to re-render.
 * Everything single-leaf (copy/retry/reload, save/remove, sources laziness)
 * stays on the leaves; the view binds it directly. Router navigation
 * (`@open-review`) stays in the view — the router is the seam between flows.
 */
export function useMessagingConsole(input: MessagingConsoleInput) {
  const { vacancyId, roundId } = input

  const messagingSummary = useMessagingSummary(vacancyId, roundId)
  const emailTemplates = useEmailTemplates(vacancyId)

  const preparedOpen = shallowRef(false)
  const preparedCandidate = shallowRef<CandidateSummary | null>(null)
  const emailTemplatesOpen = shallowRef(false)
  // Closing the templates dialog re-renders the prepared messages against the
  // edited templates — the revision never leaves the console.
  const templatesRevision = shallowRef(0)

  watch(emailTemplatesOpen, (isOpen, wasOpen) => {
    if (wasOpen && !isOpen) {
      templatesRevision.value += 1
    }
  })

  const sendCandidates = computed(() =>
    sendScope(preparedCandidate.value, messagingSummary.summaries.value ?? []),
  )
  const sendRoundName = computed(() => {
    const round = input.selectedRound.value
    return round ? roundDisplayName(round) : 'the selected round'
  })

  const preparedMessages = usePreparedMessages(
    vacancyId,
    sendCandidates,
    preparedOpen,
    templatesRevision,
  )

  function openPrepared(candidate?: CandidateSummary) {
    preparedCandidate.value = candidate ?? null
    preparedOpen.value = true
  }

  function openEmailTemplates() {
    emailTemplatesOpen.value = true
  }

  return {
    // The composed leaves: the view binds their state and intents directly.
    messagingSummary,
    preparedMessages,
    emailTemplates,
    // Console-owned wiring: open flags (the prepared one is a constructor
    // input to usePreparedMessages), the send-scope derivation, open intents.
    preparedOpen,
    emailTemplatesOpen,
    sendCandidates,
    sendRoundName,
    openPrepared,
    openEmailTemplates,
  }
}
