<script setup lang="ts">
import type { CandidateReviewStatus, CandidateSummary } from '../api'
import { formatDate } from '../format'

defineProps<{
  candidates: CandidateSummary[]
}>()

const reviewStatusLabels: Record<CandidateReviewStatus, string> = {
  new: 'New',
  flagged: 'Flagged',
  shortlisted: 'Shortlisted',
  rejected: 'Rejected',
}

/** Best available display name until CV extraction fills in the candidate details. */
function displayName(candidate: CandidateSummary): string {
  return (
    candidate.fullName ??
    candidate.sourceSenderName ??
    candidate.sourceSenderEmail ??
    'Unknown candidate'
  )
}
</script>

<template>
  <ul class="candidates">
    <li v-for="candidate in candidates" :key="candidate.id" class="candidates__row">
      <div class="candidates__main">
        <div class="candidates__heading">
          <span class="candidates__name">{{ displayName(candidate) }}</span>
          <span
            class="candidates__status"
            :class="`candidates__status--${candidate.reviewStatus}`"
          >
            {{ reviewStatusLabels[candidate.reviewStatus] }}
          </span>
        </div>
        <span v-if="candidate.sourceSubject" class="candidates__subject">
          {{ candidate.sourceSubject }}
        </span>
        <span v-if="candidate.notes" class="candidates__notes">{{ candidate.notes }}</span>
      </div>
      <span v-if="candidate.sourceSentAt" class="candidates__date">
        {{ formatDate(candidate.sourceSentAt) }}
      </span>
    </li>
  </ul>
</template>

<style scoped>
.candidates {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
}

.candidates__row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-4);
  padding: var(--space-3) var(--space-5);
  border-bottom: 1px solid var(--border);
}

.candidates__row:last-child {
  border-bottom: none;
}

.candidates__main {
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
}

.candidates__heading {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  min-width: 0;
}

.candidates__name {
  font-size: var(--text-sm);
  font-weight: 600;
  color: var(--text);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.candidates__status {
  flex-shrink: 0;
  display: inline-flex;
  align-items: center;
  padding: 0.2rem 0.65rem;
  border-radius: var(--radius-pill);
  font-size: var(--text-xs);
  font-weight: 600;
  letter-spacing: 0.01em;
}

.candidates__status--new {
  background: var(--accent-soft);
  color: var(--accent);
}

.candidates__status--flagged {
  background: var(--neutral-chip-bg);
  color: var(--neutral-chip-text);
}

.candidates__status--shortlisted {
  background: var(--success-soft);
  color: var(--success);
}

.candidates__status--rejected {
  background: var(--danger-soft);
  color: var(--danger);
}

.candidates__subject {
  font-size: var(--text-xs);
  color: var(--muted);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.candidates__notes {
  font-size: var(--text-xs);
  color: var(--neutral-chip-text);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.candidates__date {
  flex-shrink: 0;
  font-size: var(--text-xs);
  color: var(--muted);
  font-variant-numeric: tabular-nums;
}
</style>
