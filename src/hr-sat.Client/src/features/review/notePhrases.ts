/**
 * Canned phrases — the "template words" HR marks a candidate's Notes with
 * during triage. Two chip groups below the Notes editor, named after the
 * hire-plan intent they capture: Replacements — the candidate backfills a
 * runaway or resigned hire; Additionals — the candidate adds headcount
 * (additional manpower). Replacements overwrite the selection (or the whole
 * draft when nothing is selected), Additionals insert at the caret. The
 * phrases are client-side constants; notes themselves stay free text owned
 * by the server slice.
 *
 * Shortcut contract: chips are numbered across both groups in display order,
 * and Alt+<index> inserts that phrase while Notes is focused
 * (useReviewShortcuts owns the binding; ShortcutsHelpModal documents it).
 */
export type NotePhraseMode = 'replace' | 'append'

export interface NotePhrase {
  /** 1-based chip number, shown on the chip and bound to Alt+<index>. */
  index: number
  text: string
  mode: NotePhraseMode
  group: 'replacements' | 'additionals'
}

export interface NotePhraseGroup {
  id: 'replacements' | 'additionals'
  label: string
  phrases: NotePhrase[]
}

// Replacements — the candidate backfills a runaway or resigned hire.
const replacementTexts = ['Replacement — runaway', 'Replacement — resigned']
// Additionals — the candidate adds headcount beyond the planned hires.
const additionalTexts = ['Additional manpower']

function resolveGroups(): NotePhraseGroup[] {
  const groups: Array<{
    id: NotePhraseGroup['id']
    label: string
    mode: NotePhraseMode
    items: string[]
  }> = [
    { id: 'replacements', label: 'Replacements', mode: 'replace', items: replacementTexts },
    { id: 'additionals', label: 'Additionals', mode: 'append', items: additionalTexts },
  ]
  let index = 1
  return groups.map((group) => ({
    id: group.id,
    label: group.label,
    phrases: group.items.map((text) => {
      const phrase: NotePhrase = { index, text, mode: group.mode, group: group.id }
      index += 1
      return phrase
    }),
  }))
}

export const notePhraseGroups: NotePhraseGroup[] = resolveGroups()

/** All phrases in display order; the Alt+digit binding resolves through this list. */
export const notePhrases: NotePhrase[] = notePhraseGroups.flatMap((group) => group.phrases)

export function notePhraseByIndex(index: number): NotePhrase | null {
  const phrase = notePhrases[index - 1]
  return phrase !== undefined && phrase.index === index ? phrase : null
}

export interface NotePhraseInsert {
  text: string
  /** Collapsed caret position after the insert. */
  cursor: number
}

/**
 * Pure insertion rules behind the chips and the Alt+digit shortcut.
 * `start`/`end` are the textarea's selection. A collapsed selection lets a
 * Replacement overwrite the whole draft — the one-click "mark" path.
 * Additionals join with a single space when the caret sits right behind a
 * non-space character. Every result clamps to the notes maxlength.
 */
export function applyNotePhrase(
  current: string,
  start: number,
  end: number,
  phrase: NotePhrase,
  maxLength: number,
): NotePhraseInsert {
  if (phrase.mode === 'replace' && start === end) {
    const text = phrase.text.slice(0, maxLength)
    return { text, cursor: text.length }
  }

  const insert =
    phrase.mode === 'append' && start > 0 && !/\s/.test(current[start - 1] ?? '')
      ? ` ${phrase.text}`
      : phrase.text
  const text = `${current.slice(0, start)}${insert}${current.slice(end)}`.slice(0, maxLength)
  return { text, cursor: Math.min(start + insert.length, text.length) }
}
