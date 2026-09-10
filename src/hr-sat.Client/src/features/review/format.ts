/** HH:MM in the user's locale, 24-hour — for the notes "Saved HH:MM" whisper. */
export function formatClockTime(value: Date): string {
  return value.toLocaleTimeString(undefined, {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  })
}

export function formatPromotionHistory(
  promotedFromRoundNumber: number | null | undefined,
  promotedAt: string | null | undefined,
): string | null {
  if (promotedFromRoundNumber == null || promotedAt == null) {
    return null
  }

  const date = new Date(promotedAt)
  const dateLabel = Number.isNaN(date.getTime())
    ? promotedAt.slice(0, 10)
    : date.toISOString().slice(0, 10)
  return `Promoted from Round ${promotedFromRoundNumber} on ${dateLabel}.`
}
