<script setup lang="ts">
import { computed } from 'vue'
import IconButton from '@/shared/ui/IconButton.vue'
import type { CandidateReviewStatus, CandidateSummary } from '../api'
import { candidateDisplayName } from '../format'

const props = withDefaults(
  defineProps<{
    candidates: CandidateSummary[]
    readonly?: boolean
  }>(),
  { readonly: false },
)

const emit = defineEmits<{
  remove: [candidate: CandidateSummary]
}>()

const reviewStatusLabels: Record<CandidateReviewStatus, string> = {
  new: 'New',
  flagged: 'Flagged',
  shortlisted: 'Shortlisted',
  rejected: 'Rejected',
}

const gridColumns = computed(() =>
  props.readonly
    ? 'minmax(0, 1.6fr) minmax(0, 0.9fr) minmax(0, 1.4fr) minmax(0, 0.8fr)'
    : 'minmax(0, 1.6fr) minmax(0, 0.9fr) minmax(0, 1.4fr) minmax(0, 0.8fr) 4.5rem',
)
</script>

<template>
  <div class="ctable" :style="{ '--ctable-columns': gridColumns }">
    <div class="ctable__head">
      <span class="ctable__col">Candidate</span>
      <span class="ctable__col">Match Status</span>
      <span class="ctable__col">Notes</span>
      <span class="ctable__col">Status</span>
      <span v-if="!readonly" class="ctable__col ctable__col--actions">
        <span class="sr-only">Actions</span>
      </span>
    </div>

    <ul class="ctable__body">
      <li v-for="candidate in candidates" :key="candidate.id" class="crow">
        <div class="ctable__col crow__name">
          <span class="crow__name-text">{{ candidateDisplayName(candidate) }}</span>
        </div>

        <div class="ctable__col">
          <!-- Real match status lands with PDF extraction (ticket 05). -->
          <span class="crow__placeholder">—</span>
        </div>

        <div class="ctable__col">
          <span v-if="candidate.notes" class="crow__notes">{{ candidate.notes }}</span>
          <span v-else class="crow__placeholder">—</span>
        </div>

        <div class="ctable__col">
          <span class="crow__status" :class="`crow__status--${candidate.reviewStatus}`">
            {{ reviewStatusLabels[candidate.reviewStatus] }}
          </span>
        </div>

        <div v-if="!readonly" class="ctable__col ctable__col--actions">
          <div class="crow__actions">
            <IconButton
              icon="send"
              label="Send email — available once email templates exist"
              disabled
            />
            <IconButton
              icon="trash"
              label="Delete candidate"
              variant="danger"
              @click="emit('remove', candidate)"
            />
          </div>
        </div>
      </li>
    </ul>
  </div>
</template>

<style scoped>
.ctable {
  display: flex;
  flex-direction: column;
}

.ctable__head {
  display: grid;
  grid-template-columns: var(--ctable-columns);
  gap: var(--space-4);
  align-items: center;
  padding: 0 var(--space-5) var(--space-3);
  border-bottom: 1px solid var(--border);
}

.ctable__head .ctable__col {
  font-size: var(--text-xs);
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: var(--muted);
}

.ctable__body {
  list-style: none;
  margin: 0;
  padding: 0;
}

.crow {
  display: grid;
  grid-template-columns: var(--ctable-columns);
  gap: var(--space-4);
  align-items: center;
  padding: var(--space-4) var(--space-5);
  border-bottom: 1px solid var(--border);
  transition: background 0.15s ease;
}

.crow:last-child {
  border-bottom: none;
}

.crow:hover {
  background: var(--surface-subtle);
}

.ctable__col {
  min-width: 0;
}

.crow__name-text {
  display: block;
  font-size: var(--text-sm);
  font-weight: 600;
  color: var(--text);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.crow__notes {
  display: block;
  font-size: var(--text-xs);
  color: var(--neutral-chip-text);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.crow__placeholder {
  font-size: var(--text-xs);
  color: var(--muted);
}

.crow__status {
  display: inline-flex;
  align-items: center;
  padding: 0.2rem 0.65rem;
  border-radius: var(--radius-pill);
  font-size: var(--text-xs);
  font-weight: 600;
  letter-spacing: 0.01em;
}

.crow__status--new {
  background: var(--accent-soft);
  color: var(--accent);
}

.crow__status--flagged {
  background: var(--neutral-chip-bg);
  color: var(--neutral-chip-text);
}

.crow__status--shortlisted {
  background: var(--success-soft);
  color: var(--success);
}

.crow__status--rejected {
  background: var(--danger-soft);
  color: var(--danger);
}

.ctable__col--actions {
  justify-self: end;
}

.crow__actions {
  display: inline-flex;
  align-items: center;
  gap: var(--space-1);
}

.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: 0;
}
</style>
