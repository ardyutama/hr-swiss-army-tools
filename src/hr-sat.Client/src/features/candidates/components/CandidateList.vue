<script setup lang="ts">
import { computed } from 'vue'
import type {
  CandidateHireOutcome,
  CandidateReviewStatus,
  CandidateSummary,
} from '../api'
import type { ReceivedSort } from '../filter'
import {
  candidateDisplayName,
  contactability,
  contactBlockReason,
  formatReceivedAt,
} from '../format'

const props = withDefaults(
  defineProps<{
    vacancyId: string
    candidates: CandidateSummary[]
    requirements: string[]
    receivedSort: ReceivedSort
    /** Current 1-based page; the list is server-paged. */
    page: number
    pageSize: number
    /** The filtered in-scope total behind the pager and the position counter. */
    total: number
    /** A page transition is in flight: chrome holds, skeleton rows swap in. */
    pending?: boolean
    readonly?: boolean
  }>(),
  { pending: false, readonly: false },
)

const emit = defineEmits<{
  remove: [candidate: CandidateSummary]
  review: [candidate: CandidateSummary]
  send: [candidate: CandidateSummary]
  toggleReceivedSort: []
  'update:page': [page: number]
}>()

function sendLabel(candidate: CandidateSummary): string {
  const classification = contactability(candidate)
  return classification.kind === 'contactable'
    ? 'Send email'
    : `Send email — ${contactBlockReason(classification)}`
}

/** Decision 3 (issue 04): muted glyph before the name identifies the intake source. */
const sourceIcons: Record<string, string> = {
  email: 'i-lucide-mail',
  form: 'i-lucide-file-text',
}

const sourceLabels: Record<string, string> = {
  email: 'Source Email',
  form: 'Form Response',
}

function sourceIcon(candidate: CandidateSummary): string {
  return sourceIcons[candidate.intakeSource] ?? 'i-lucide-help-circle'
}

function sourceLabel(candidate: CandidateSummary): string {
  return sourceLabels[candidate.intakeSource] ?? candidate.intakeSource
}

const reviewStatusLabels: Record<CandidateReviewStatus, string> = {
  new: 'New',
  flagged: 'Flagged',
  shortlisted: 'Shortlisted',
  rejected: 'Rejected',
}

const reviewStatusColors: Record<CandidateReviewStatus, 'success' | 'error' | 'neutral' | 'primary'> = {
  new: 'primary',
  flagged: 'neutral',
  shortlisted: 'success',
  rejected: 'error',
}

const hireOutcomeLabels: Record<Exclude<CandidateHireOutcome, 'none'>, string> = {
  hired: 'Hired',
  runaway: 'Runaway',
  declined: 'Declined',
}

const hireOutcomeColors: Record<Exclude<CandidateHireOutcome, 'none'>, 'success' | 'error' | 'neutral'> = {
  hired: 'success',
  runaway: 'error',
  declined: 'neutral',
}

// Column proportions applied via <colgroup> so the semantic table keeps a fixed layout.
// Order is fixed by the S3 sketch: Candidate | Received | CV | Notes | Review status | Actions.
const columnWidths = ['24%', '14%', '8%', '28%', '12%', '7.5rem']

// The pager only exists when a second page does — a one-page round is not dead UI.
const showPagination = computed(() => props.total > props.pageSize)
const pageStart = computed(() => (props.page - 1) * props.pageSize + 1)
const pageEnd = computed(() => Math.min(props.page * props.pageSize, props.total))

/** At most two rule chips render; the rest collapse into a "+N more rules" chip. */
const MAX_VISIBLE_RULE_CHIPS = 2

function visibleRules(candidate: CandidateSummary) {
  return candidate.firedRules.slice(0, MAX_VISIBLE_RULE_CHIPS)
}

function collapsedRuleCount(candidate: CandidateSummary): number {
  return candidate.firedRules.length - MAX_VISIBLE_RULE_CHIPS
}
</script>

<template>
  <div>
    <div
      v-if="requirements.length > 0"
      class="border-b border-default px-5 py-3"
    >
      <div
        class="flex flex-wrap items-center gap-2"
        role="group"
        aria-label="Skills requirements"
      >
        <span class="shrink-0 text-xs font-semibold uppercase tracking-[0.06em] text-muted">Skills requirements</span>
        <div class="flex flex-wrap gap-1.5">
          <UBadge
            v-for="requirement in requirements"
            :key="requirement"
            color="neutral"
            variant="subtle"
          >
            {{ requirement }}
          </UBadge>
        </div>
      </div>
    </div>

    <div class="overflow-x-auto">
      <table class="ctable w-full min-w-[56rem] table-fixed border-collapse">
      <colgroup>
        <col v-for="width in columnWidths" :key="width" :style="{ width }" />
      </colgroup>
      <thead class="ctable__head">
        <tr>
          <th class="ctable__col border-b border-default px-2 pb-3 pt-2 text-left text-xs font-semibold uppercase tracking-[0.06em] text-muted first:pl-5 last:pr-5" scope="col">Candidate</th>
          <th
            class="ctable__col border-b border-default px-2 pb-3 pt-2 text-left text-xs font-semibold uppercase tracking-[0.06em] text-muted first:pl-5 last:pr-5"
            scope="col"
            :aria-sort="receivedSort === 'oldest' ? 'ascending' : 'descending'"
          >
            <button
              type="button"
              class="-mx-1 -my-0.5 inline-flex cursor-pointer items-center gap-1 rounded-md px-1 py-0.5 uppercase tracking-[0.06em] transition-colors hover:text-highlighted focus-visible:outline-2 focus-visible:outline-primary"
              :title="receivedSort === 'oldest' ? 'Oldest first - click for newest first' : 'Newest first - click for oldest first'"
              @click="emit('toggleReceivedSort')"
            >
              Received
              <UIcon
                :name="receivedSort === 'oldest' ? 'i-lucide-arrow-up' : 'i-lucide-arrow-down'"
                class="size-3.5"
                aria-hidden="true"
              />
            </button>
          </th>
          <th class="ctable__col border-b border-default px-2 pb-3 pt-2 text-left text-xs font-semibold uppercase tracking-[0.06em] text-muted first:pl-5 last:pr-5" scope="col">CV</th>
          <th class="ctable__col border-b border-default px-2 pb-3 pt-2 text-left text-xs font-semibold uppercase tracking-[0.06em] text-muted first:pl-5 last:pr-5" scope="col">Notes</th>
          <th class="ctable__col border-b border-default px-2 pb-3 pt-2 text-left text-xs font-semibold uppercase tracking-[0.06em] text-muted first:pl-5 last:pr-5" scope="col">Review status</th>
          <th class="ctable__col ctable__col--actions border-b border-default px-2 pb-3 pt-0 text-left text-xs font-semibold uppercase tracking-[0.06em] text-muted first:pl-5 last:pr-5" scope="col">
            <span class="sr-only">Actions</span>
          </th>
        </tr>
      </thead>

      <!-- Page transitions keep the chrome stable and swap in shape-matched
           skeleton rows (ADR-0008 #14) — no full-panel spinner. -->
      <tbody v-if="pending" aria-busy="true" aria-label="Loading candidates">
        <tr v-for="n in 4" :key="n" class="crow">
          <td class="ctable__col border-b border-default px-2 py-4 first:pl-5 last:pr-5"><USkeleton class="h-4 w-3/4" /></td>
          <td class="ctable__col border-b border-default px-2 py-4 first:pl-5 last:pr-5"><USkeleton class="h-3 w-16" /></td>
          <td class="ctable__col border-b border-default px-2 py-4 first:pl-5 last:pr-5"><USkeleton class="h-4 w-4" /></td>
          <td class="ctable__col border-b border-default px-2 py-4 first:pl-5 last:pr-5"><USkeleton class="h-3 w-1/2" /></td>
          <td class="ctable__col border-b border-default px-2 py-4 first:pl-5 last:pr-5"><USkeleton class="h-5 w-20 rounded-full" /></td>
          <td class="ctable__col border-b border-default px-2 py-4 first:pl-5 last:pr-5" />
        </tr>
      </tbody>

      <tbody v-else>
        <tr
          v-for="candidate in candidates"
          :key="candidate.id"
          class="crow group cursor-pointer focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-primary"
          tabindex="0"
          :aria-label="`Open ${candidateDisplayName(candidate)} for review`"
          @click="emit('review', candidate)"
          @keydown.enter.prevent="emit('review', candidate)"
          @keydown.space.prevent="emit('review', candidate)"
        >
            <td class="ctable__col crow__name border-b border-default px-2 py-4 align-middle transition-colors first:pl-5 last:pr-5 group-hover:bg-muted">
            <span class="flex min-w-0 items-center gap-1.5">
              <UTooltip :text="sourceLabel(candidate)">
                <UIcon
                  :name="sourceIcon(candidate)"
                  class="size-4 shrink-0 text-muted"
                  :aria-label="sourceLabel(candidate)"
                />
              </UTooltip>
              <span class="crow__name-text block max-w-full truncate text-sm font-semibold text-highlighted">{{ candidateDisplayName(candidate) }}</span>
            </span>
            <!-- Fired-rule chips stack under the name: no new column, the
                 colgroup never reflows on toggle (decision 26). -->
            <div
              v-if="candidate.firedRules.length > 0"
              class="mt-1 flex max-w-full flex-col items-start gap-1"
            >
              <UTooltip
                v-for="rule in visibleRules(candidate)"
                :key="rule.index"
                :text="rule.display"
              >
                <UBadge color="neutral" variant="subtle" class="crow__rule-chip max-w-full truncate">
                  {{ rule.display }}
                </UBadge>
              </UTooltip>
              <UTooltip v-if="collapsedRuleCount(candidate) > 0">
                <UBadge color="neutral" variant="outline">
                  +{{ collapsedRuleCount(candidate) }} more rules
                </UBadge>
                <template #content>
                  <ul class="flex max-w-72 flex-col gap-1">
                    <li
                      v-for="rule in candidate.firedRules"
                      :key="rule.index"
                    >{{ rule.display }}</li>
                  </ul>
                </template>
              </UTooltip>
            </div>
          </td>

          <td class="ctable__col crow__received border-b border-default px-2 py-4 align-middle transition-colors first:pl-5 last:pr-5 group-hover:bg-muted">
            <span class="block truncate text-xs tabular-nums text-muted">{{ formatReceivedAt(candidate.sourceSentAt) }}</span>
          </td>

          <td class="ctable__col crow__cv border-b border-default px-2 py-4 align-middle transition-colors first:pl-5 last:pr-5 group-hover:bg-muted">
            <!-- Decision 1 (issue 04): the CV cell is a four-state matrix. Form
                 candidates carry a Drive link, never a local PDF, so they must not
                 show the email "No CV" gap badge. -->
            <template v-if="candidate.intakeSource === 'form'">
              <UButton
                v-if="candidate.cvLink"
                :to="candidate.cvLink"
                target="_blank"
                rel="noopener"
                icon="i-lucide-external-link"
                color="neutral"
                variant="link"
                class="px-0"
                title="Open CV link in a new tab"
                @click.stop
              >Link</UButton>
              <UBadge v-else color="warning" variant="subtle" icon="i-lucide-file-warning">No CV link</UBadge>
            </template>
            <template v-else>
              <span
                v-if="candidate.cvDocumentCount > 0"
                class="inline-flex items-center text-muted"
                :title="candidate.cvDocumentCount === 1 ? '1 CV document' : `${candidate.cvDocumentCount} CV documents`"
              >
                <UIcon name="i-lucide-paperclip" class="size-4" aria-hidden="true" />
                <span class="sr-only">{{ candidate.cvDocumentCount === 1 ? '1 CV document' : `${candidate.cvDocumentCount} CV documents` }}</span>
              </span>
              <UBadge v-else color="warning" variant="subtle" icon="i-lucide-file-warning">No CV</UBadge>
            </template>
          </td>

          <td class="ctable__col border-b border-default px-2 py-4 align-middle transition-colors first:pl-5 last:pr-5 group-hover:bg-muted">
            <span v-if="candidate.notes" class="crow__notes block truncate text-xs text-muted">{{ candidate.notes }}</span>
            <span v-else class="crow__placeholder text-xs text-muted">—</span>
          </td>

          <td class="ctable__col border-b border-default px-2 py-4 align-middle transition-colors first:pl-5 last:pr-5 group-hover:bg-muted">
            <div class="flex flex-wrap items-center gap-1.5">
              <UBadge :color="reviewStatusColors[candidate.reviewStatus]" variant="subtle">
                {{ reviewStatusLabels[candidate.reviewStatus] }}
              </UBadge>
              <UBadge
                v-if="candidate.hireOutcome !== 'none'"
                :color="hireOutcomeColors[candidate.hireOutcome]"
                variant="subtle"
              >
                {{ hireOutcomeLabels[candidate.hireOutcome] }}
              </UBadge>
              <UBadge v-if="candidate.isResubmitted" color="neutral" variant="subtle">
                Resubmitted
              </UBadge>
            </div>
          </td>

          <td class="ctable__col ctable__col--actions border-b border-default px-2 py-4 text-right align-middle transition-colors first:pl-5 last:pr-5 group-hover:bg-muted">
            <div class="crow__actions inline-flex items-center gap-1">
              <template v-if="!readonly">
                <UButton
                  icon="i-lucide-send"
                  color="neutral"
                  variant="ghost"
                  :aria-label="sendLabel(candidate)"
                  :title="sendLabel(candidate)"
                  :disabled="contactability(candidate).kind !== 'contactable'"
                  @click.stop="emit('send', candidate)"
                />
                <!-- Screened-out candidates can never be deleted (decision 7);
                     the row stays fully clickable into review. -->
                <UButton
                  v-if="!candidate.screenedOut"
                  icon="i-lucide-trash-2"
                  color="error"
                  variant="ghost"
                  aria-label="Delete candidate"
                  title="Delete candidate"
                  @click.stop="emit('remove', candidate)"
                />
              </template>
              <UIcon
                name="i-lucide-chevron-right"
                class="crow__chevron size-4 shrink-0 text-muted transition-transform group-hover:translate-x-0.5 group-hover:text-highlighted"
                aria-hidden="true"
              />
            </div>
          </td>
        </tr>
      </tbody>
      </table>
    </div>

    <div
      v-if="showPagination"
      class="flex items-center justify-between gap-3 border-t border-default px-5 py-3"
    >
      <p class="text-sm tabular-nums text-muted">{{ pageStart }}-{{ pageEnd }} of {{ total }}</p>
      <UPagination
        :page="page"
        :items-per-page="pageSize"
        :total="total"
        @update:page="emit('update:page', $event)"
      />
    </div>
  </div>
</template>

<style scoped>
.crow:last-child .ctable__col {
  border-bottom: none;
}
</style>
