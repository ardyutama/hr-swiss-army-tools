/** HH:MM in the user's locale, 24-hour — for the notes "Saved HH:MM" whisper. */
export function formatClockTime(value: Date): string {
  return value.toLocaleTimeString(undefined, {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  })
}
