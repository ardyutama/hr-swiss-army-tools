import { describe, expect, it } from 'vitest'
import {
  applyNotePhrase,
  notePhraseByIndex,
  notePhraseGroups,
  notePhrases,
  type NotePhrase,
} from './notePhrases'

const MAX = 4000

const replacement: NotePhrase = {
  index: 1,
  text: 'Strong fit',
  mode: 'replace',
  group: 'replacements',
}
const additional: NotePhrase = {
  index: 4,
  text: 'Follow up by phone',
  mode: 'append',
  group: 'additionals',
}

describe('applyNotePhrase', () => {
  it('a Replacement with a collapsed caret overwrites the whole draft', () => {
    expect(applyNotePhrase('half-typed thought', 4, 4, replacement, MAX)).toEqual({
      text: 'Strong fit',
      cursor: 'Strong fit'.length,
    })
  })

  it('a Replacement overwrites only the selected span', () => {
    expect(applyNotePhrase('maybe later', 0, 5, replacement, MAX)).toEqual({
      text: 'Strong fit later',
      cursor: 'Strong fit'.length,
    })
  })

  it('an Additional appends at the caret with a single joining space', () => {
    const draft = 'Strong on VAT'
    expect(applyNotePhrase(draft, draft.length, draft.length, additional, MAX)).toEqual({
      text: 'Strong on VAT Follow up by phone',
      cursor: 'Strong on VAT Follow up by phone'.length,
    })
  })

  it('an Additional adds no extra space behind existing whitespace', () => {
    const draft = 'Strong on VAT '
    expect(applyNotePhrase(draft, draft.length, draft.length, additional, MAX)).toEqual({
      text: 'Strong on VAT Follow up by phone',
      cursor: 'Strong on VAT Follow up by phone'.length,
    })
  })

  it('an Additional into an empty draft inserts bare text', () => {
    expect(applyNotePhrase('', 0, 0, additional, MAX)).toEqual({
      text: 'Follow up by phone',
      cursor: 'Follow up by phone'.length,
    })
  })

  it('an Additional joins with a space at a mid-text caret behind a non-space', () => {
    expect(applyNotePhrase('call soon', 4, 4, additional, MAX)).toEqual({
      text: 'call Follow up by phone soon',
      cursor: 'call Follow up by phone'.length,
    })
  })

  it('clamps the result to the notes maxlength', () => {
    const draft = 'x'.repeat(MAX)
    const result = applyNotePhrase(draft, draft.length, draft.length, additional, MAX)
    expect(result.text).toHaveLength(MAX)
    expect(result.cursor).toBeLessThanOrEqual(MAX)
  })
})

describe('notePhrases', () => {
  it('numbers the chips across both groups starting at 1', () => {
    expect(notePhrases.map((phrase) => phrase.index)).toEqual(
      notePhrases.map((_, position) => position + 1),
    )
    expect(notePhraseGroups[0]?.phrases[0]?.index).toBe(1)
    const additions = notePhraseGroups[1]?.phrases ?? []
    expect(additions[0]?.index).toBe((notePhraseGroups[0]?.phrases.length ?? 0) + 1)
  })

  it('resolves shortcuts by 1-based index, null out of range', () => {
    expect(notePhraseByIndex(1)).toEqual(notePhrases[0])
    expect(notePhraseByIndex(notePhrases.length)).toEqual(notePhrases[notePhrases.length - 1])
    expect(notePhraseByIndex(0)).toBeNull()
    expect(notePhraseByIndex(notePhrases.length + 1)).toBeNull()
  })
})
