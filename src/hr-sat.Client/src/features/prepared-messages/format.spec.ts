import { describe, expect, it } from 'vitest'
import type { CandidateSummary } from '@/features/candidates/api'
import { sendScope } from './format'

function candidate(id: number): CandidateSummary {
  return {
    id,
    fullName: `Candidate ${id}`,
    contactEmail: `candidate-${id}@example.com`,
    notes: null,
    reviewStatus: 'shortlisted',
    hireOutcome: 'none',
    sourceSenderName: null,
    sourceSenderEmail: null,
    sourceSubject: null,
    sourceSentAt: null,
    cvDocumentCount: 0,
  }
}

describe('US-19: prepared message send scope', () => {
  it('narrows a row send to the selected candidate', () => {
    const target = candidate(2)
    const roundCandidates = [candidate(1), target, candidate(3)]

    expect(sendScope(target, roundCandidates)).toEqual([target])
  })

  it('keeps the complete selected round for Send To All', () => {
    const roundCandidates = [candidate(1), candidate(2), candidate(3)]

    expect(sendScope(null, roundCandidates)).toBe(roundCandidates)
  })
})