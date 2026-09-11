import { computed, shallowRef, type Ref } from 'vue'
import { useToast } from '@nuxt/ui/composables/useToast'
import type { VacancyRound } from '@/features/vacancies/api'
import { problemMessage } from '@/shared/problem-details'
import { closeRound, createRound } from './api'

/** Round label used across the round chrome: "Round N" plus the optional name. */
export function roundDisplayName(round: Pick<VacancyRound, 'roundNumber' | 'name'>): string {
  const base = `Round ${round.roundNumber}`
  const name = round.name?.trim()
  return name ? `${base} — ${name}` : base
}

export type { VacancyRound }

/**
 * Owns the intake-round chrome for the vacancy-detail flow: which round is
 * active, whether a new round can be opened, and the create/close mutations.
 * The rounds themselves are source-of-truthed by `useVacancyDetail`; this
 * composable only derives round state and re-runs `onChanged` after a mutation
 * so the vacancy rollup (and its embedded rounds) refresh.
 */
export function useIntakeRounds(
  rounds: Ref<VacancyRound[]>,
  vacancyId: Ref<string>,
  onChanged: () => Promise<void>,
) {
  const toast = useToast()
  const creating = shallowRef(false)
  const closing = shallowRef(false)

  const activeRound = computed(() => rounds.value.find((round) => round.status === 'open') ?? null)

  // A new round can only open while none is active; the vacancy-close freeze is
  // enforced server-side, so this stays a pure round-state derivation.
  const canCreateRound = computed(() => activeRound.value === null)

  async function create(name: string | null): Promise<VacancyRound | null> {
    if (creating.value || !canCreateRound.value) {
      return null
    }
    creating.value = true
    try {
      const created = await createRound(vacancyId.value, name)
      toast.add({ title: `${roundDisplayName(created)} opened`, color: 'success' })
      await onChanged()
      return created
    } catch (error) {
      const message = problemMessage(error, "Couldn't open the round")
      toast.add({
        title: message.title,
        description: message.description,
        color: message.color,
      })
      if (message.kind !== 'failure') {
        await onChanged()
      }
      return null
    } finally {
      creating.value = false
    }
  }

  async function close(round: VacancyRound): Promise<boolean> {
    if (closing.value || round.status !== 'open') {
      return false
    }
    closing.value = true
    try {
      await closeRound(vacancyId.value, round.id)
      toast.add({ title: `${roundDisplayName(round)} closed`, color: 'success' })
      await onChanged()
      return true
    } catch (error) {
      const message = problemMessage(error, "Couldn't close the round", {
        round: roundDisplayName(round),
      })
      toast.add({
        title: message.title,
        description: message.description,
        color: message.color,
      })
      if (message.kind !== 'failure') {
        await onChanged()
      }
      return false
    } finally {
      closing.value = false
    }
  }

  return { creating, closing, activeRound, canCreateRound, create, close }
}
