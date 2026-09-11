import { fieldErrorsOf } from './validation'

export type ProblemMessageColor = 'warning' | 'error'
export type ProblemMessageKind = 'lifecycle' | 'conflict' | 'failure'

export interface ProblemMessage {
  title: string
  description: string
  color: ProblemMessageColor
  kind: ProblemMessageKind
}

export interface ProblemMessageOptions {
  round?: string
  conflictDescription?: string
}

const closedRoundDescription =
  'Closed rounds are read-only. To keep working with its candidates, promote them into the active round.'
const notClosedRoundDescription =
  "You can only promote from closed rounds. Close it first, or pick another round."

export function problemMessage(
  error: unknown,
  fallback: string,
  options: ProblemMessageOptions = {},
): ProblemMessage {
  const apiError = apiErrorParts(error)
  const code = problemTitle(apiError?.problem)

  if (code === 'IntakeRounds.Closed') {
    return lifecycleMessage(
      options.round ? `${options.round} is closed` : 'This round is closed',
      closedRoundDescription,
    )
  }

  if (code === 'IntakeRounds.NoActiveRound') {
    return lifecycleMessage(
      'No active round',
      'Open a round first, then promote these candidates into it.',
    )
  }

  if (code === 'IntakeRounds.ActiveRoundExists') {
    return lifecycleMessage(
      'A round is already active',
      'Close the active round before opening a new one.',
    )
  }

  if (code === 'IntakeRounds.NotClosed') {
    return lifecycleMessage(
      options.round ? `${options.round} isn't closed yet` : "This round isn't closed yet",
      notClosedRoundDescription,
    )
  }

  if (code === 'Vacancies.Closed') {
    return lifecycleMessage('This vacancy is closed', 'Reopen the vacancy to record hire outcomes.')
  }

  // A 400 ValidationProblem the call site did not pre-handle is the server
  // refusing the change on rule grounds (a settled record or failed
  // precondition): surface its first field message verbatim as a warning.
  if (apiError?.status === 400) {
    const firstFieldError = Object.values(fieldErrorsOf(error) ?? {})
      .flat()
      .find((message) => message.length > 0)
    if (firstFieldError) {
      return {
        title: "That change isn't allowed",
        description: firstFieldError,
        color: 'warning',
        kind: 'lifecycle',
      }
    }
  }

  if (apiError?.status === 409) {
    return {
      title: "Couldn't save that change",
      description:
        options.conflictDescription ?? "The data changed on the server. We've refreshed -- try again.",
      color: 'error',
      kind: 'conflict',
    }
  }

  if (apiError?.status === 404) {
    return {
      title: "Couldn't find that",
      description: 'It may have been removed. Head back and reopen it.',
      color: 'error',
      kind: 'failure',
    }
  }

  return {
    title: fallback,
    description: 'Please try again.',
    color: 'error',
    kind: 'failure',
  }
}

export function problemMessageText(message: ProblemMessage): string {
  return `${message.title}: ${message.description}`
}

function lifecycleMessage(title: string, description: string): ProblemMessage {
  return { title, description, color: 'warning', kind: 'lifecycle' }
}

function apiErrorParts(error: unknown): { status: number; problem: unknown } | null {
  if (typeof error !== 'object' || error === null) {
    return null
  }

  const candidate = error as { status?: unknown; problem?: unknown }
  return typeof candidate.status === 'number'
    ? { status: candidate.status, problem: candidate.problem }
    : null
}

function problemTitle(problem: unknown): string | null {
  if (typeof problem !== 'object' || problem === null || !('title' in problem)) {
    return null
  }

  const title = (problem as { title?: unknown }).title
  return typeof title === 'string' ? title : null
}