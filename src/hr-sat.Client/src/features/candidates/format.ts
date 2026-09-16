type CandidateDisplaySource = {
  fullName: string | null
  sourceSenderName: string | null
  sourceSenderEmail: string | null
  sourceSubject?: string | null
}

export function candidateDisplayName(candidate: CandidateDisplaySource): string {
  return (
    candidate.fullName ??
    candidate.sourceSenderName ??
    candidate.sourceSenderEmail ??
    candidate.sourceSubject ??
    'Unknown candidate'
  )
}

const receivedFormatter = new Intl.DateTimeFormat(undefined, {
  year: 'numeric',
  month: 'short',
  day: 'numeric',
  hour: 'numeric',
  minute: '2-digit',
})

export function formatReceivedAt(iso: string | null): string {
  if (iso === null) {
    return '—'
  }
  const date = new Date(iso)
  return Number.isNaN(date.getTime()) ? iso : receivedFormatter.format(date)
}

type ContactEligibilitySource = {
  reviewStatus: 'new' | 'flagged' | 'shortlisted' | 'rejected'
  hireOutcome: 'none' | 'hired' | 'runaway' | 'declined'
  contactEmail: string | null
}

export type Contactability =
  | { kind: 'undecided' }
  | { kind: 'outcome-recorded' }
  | { kind: 'missing-email' }
  | { kind: 'contactable'; decidedAs: 'shortlisted' | 'rejected' }

export function contactability(candidate: ContactEligibilitySource): Contactability {
  if (candidate.reviewStatus === 'new' || candidate.reviewStatus === 'flagged') {
    return { kind: 'undecided' }
  }
  if (candidate.reviewStatus === 'shortlisted' && candidate.hireOutcome !== 'none') {
    return { kind: 'outcome-recorded' }
  }
  if (candidate.contactEmail === null || candidate.contactEmail === '') {
    return { kind: 'missing-email' }
  }
  return { kind: 'contactable', decidedAs: candidate.reviewStatus }
}

export function contactabilityPlan<T extends ContactEligibilitySource>(candidates: readonly T[]) {
  const plan: {
    contactable: T[]
    missingEmail: T[]
    undecided: T[]
    outcomeRecorded: T[]
  } = {
    contactable: [],
    missingEmail: [],
    undecided: [],
    outcomeRecorded: [],
  }

  for (const candidateToClassify of candidates) {
    const classification = contactability(candidateToClassify)
    switch (classification.kind) {
      case 'contactable':
        plan.contactable.push(candidateToClassify)
        break
      case 'missing-email':
        plan.missingEmail.push(candidateToClassify)
        break
      case 'undecided':
        plan.undecided.push(candidateToClassify)
        break
      case 'outcome-recorded':
        plan.outcomeRecorded.push(candidateToClassify)
        break
    }
  }

  return plan
}

export function contactBlockReason(
  classification: Exclude<Contactability, { kind: 'contactable' }>,
): string {
  switch (classification.kind) {
    case 'undecided':
      return 'Not yet shortlisted or rejected'
    case 'outcome-recorded':
      return 'Hire outcome already recorded'
    case 'missing-email':
      return 'Needs an email address'
  }
}
