import { describe, expect, it } from 'vitest'

import type { CandidateHireOutcome, CandidateReviewStatus } from './api'
import type { Contactability } from './format'
import { contactability, contactabilityPlan, contactBlockReason } from './format'

type TestCandidate = {
  id: string
  reviewStatus: CandidateReviewStatus
  hireOutcome: CandidateHireOutcome
  contactEmail: string | null
}

const reviewStatuses: CandidateReviewStatus[] = ['new', 'flagged', 'shortlisted', 'rejected']
const hireOutcomes: CandidateHireOutcome[] = ['none', 'hired', 'runaway', 'declined']
const emailValues = [null, 'candidate@example.com']

function candidate(
  reviewStatus: CandidateReviewStatus,
  hireOutcome: CandidateHireOutcome,
  contactEmail: string | null,
): TestCandidate {
  return {
    id: `${reviewStatus}-${hireOutcome}-${contactEmail === null ? 'missing' : 'present'}`,
    reviewStatus,
    hireOutcome,
    contactEmail,
  }
}

function expectedContactability(candidateToClassify: TestCandidate): Contactability {
  if (candidateToClassify.reviewStatus === 'new' || candidateToClassify.reviewStatus === 'flagged') {
    return { kind: 'undecided' }
  }
  if (
    candidateToClassify.reviewStatus === 'shortlisted' &&
    candidateToClassify.hireOutcome !== 'none'
  ) {
    return { kind: 'outcome-recorded' }
  }
  if (
    (candidateToClassify.contactEmail === null || candidateToClassify.contactEmail === '') &&
    (candidateToClassify.reviewStatus === 'shortlisted' ||
      candidateToClassify.reviewStatus === 'rejected')
  ) {
    return { kind: 'missing-email' }
  }
  return { kind: 'contactable', decidedAs: candidateToClassify.reviewStatus }
}

const truthTable = reviewStatuses.flatMap((reviewStatus) =>
  hireOutcomes.flatMap((hireOutcome) =>
    emailValues.map((contactEmail) => candidate(reviewStatus, hireOutcome, contactEmail)),
  ),
)

describe('domain: Contactable Candidate eligibility', () => {
  it.each(truthTable)(
    'classifies $id consistently across contactability helpers',
    (candidateToClassify) => {
      const expected = expectedContactability(candidateToClassify)
      const actual = contactability(candidateToClassify)

      expect(actual).toEqual(expected)
      if (actual.kind !== 'contactable') {
        expect(contactBlockReason(actual)).toBe(
          expected.kind === 'undecided'
            ? 'Not yet shortlisted or rejected'
            : expected.kind === 'outcome-recorded'
              ? 'Hire outcome already recorded'
              : 'Needs an email address',
        )
      }
    },
  )

  it('partitions every candidate into exactly one existing helper bucket', () => {
    const candidates = [
      candidate('new', 'none', null),
      candidate('flagged', 'hired', 'flagged@example.com'),
      candidate('shortlisted', 'none', null),
      candidate('shortlisted', 'none', 'bench@example.com'),
      candidate('shortlisted', 'hired', 'hired@example.com'),
      candidate('rejected', 'none', null),
      candidate('rejected', 'declined', 'rejected@example.com'),
    ]
    const plan = contactabilityPlan(candidates)
    const buckets = [
      plan.contactable,
      plan.missingEmail,
      plan.undecided,
      plan.outcomeRecorded,
    ].flat()

    expect(new Set(buckets.map((candidateToClassify) => candidateToClassify.id)).size).toBe(
      candidates.length,
    )
    expect(buckets.map((candidateToClassify) => candidateToClassify.id).sort()).toEqual(
      candidates.map((candidateToClassify) => candidateToClassify.id).sort(),
    )
    expect(plan.undecided).toHaveLength(2)
    expect(plan.outcomeRecorded).toHaveLength(1)
  })
})