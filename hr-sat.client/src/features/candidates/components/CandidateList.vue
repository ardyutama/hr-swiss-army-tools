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

// Column proportions applied via <colgroup> so the semantic table keeps a fixed layout.
const columnWidths = computed(() =>
  props.readonly
    ? ['34%', '19%', '30%', '17%']
    : ['30%', '17%', '26%', '14%', '4.5rem'],
)
</script>

<template>
  <table class="ctable">
    <colgroup>
      <col v-for="width in columnWidths" :key="width" :style="{ width }" />
    </colgroup>
    <thead class="ctable__head">
      <tr>
        <th class="ctable__col" scope="col">Candidate</th>
        <th class="ctable__col" scope="col">Match Status</th>
        <th class="ctable__col" scope="col">Notes</th>
        <th class="ctable__col" scope="col">Status</th>
        <th v-if="!readonly" class="ctable__col ctable__col--actions" scope="col">
          <span class="sr-only">Actions</span>
        </th>
      </tr>
    </thead>

    <tbody>
      <tr v-for="candidate in candidates" :key="candidate.id" class="crow">
        <td class="ctable__col crow__name">
          <span class="crow__name-text">{{ candidateDisplayName(candidate) }}</span>
        </td>

        <td class="ctable__col">
          <!-- Real match status lands with PDF extraction (ticket 05). -->
          <span class="crow__placeholder">—</span>
        </td>

        <td class="ctable__col">
          <span v-if="candidate.notes" class="crow__notes">{{ candidate.notes }}</span>
          <span v-else class="crow__placeholder">—</span>
        </td>

        <td class="ctable__col">
          <span class="crow__status" :class="`crow__status--${candidate.reviewStatus}`">
            {{ reviewStatusLabels[candidate.reviewStatus] }}
          </span>
        </td>

        <td v-if="!readonly" class="ctable__col ctable__col--actions">
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
        </td>
      </tr>
    </tbody>
  </table>
</template>

<style scoped>
.ctable {
  width: 100%;
  border-collapse: collapse;
  table-layout: fixed;
}

.ctable__col {
  padding: var(--space-4) var(--space-2);
  vertical-align: middle;
  transition: background 0.15s ease;
}

.ctable__col:first-child {
  padding-left: var(--space-5);
}

.ctable__col:last-child {
  padding-right: var(--space-5);
}

.ctable__head .ctable__col {
  padding-top: 0;
  padding-bottom: var(--space-3);
  border-bottom: 1px solid var(--border);
  font-size: var(--text-xs);
  font-weight: 600;
  text-align: left;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: var(--muted);
}

.crow .ctable__col {
  border-bottom: 1px solid var(--border);
}

.crow:last-child .ctable__col {
  border-bottom: none;
}

.crow:hover .ctable__col {
  background: var(--surface-subtle);
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
  text-align: right;
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
