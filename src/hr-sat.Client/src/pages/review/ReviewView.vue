<script setup lang="ts">
import { computed, shallowRef, toRef, useTemplateRef, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useToast } from '@nuxt/ui/composables/useToast'
import { candidateDisplayName } from '@/features/candidates/format'
import ReviewHeader from '@/features/review/components/ReviewHeader.vue'
import RequirementsPanel from '@/features/review/components/RequirementsPanel.vue'
import CandidateDetailsPanel from '@/features/review/components/CandidateDetailsPanel.vue'
import SourceEmailPanel from '@/features/review/components/SourceEmailPanel.vue'
import NotesEditor from '@/features/review/components/NotesEditor.vue'
import CvViewer from '@/features/review/components/CvViewer.vue'
import ReviewActionBar from '@/features/review/components/ReviewActionBar.vue'
import OutcomeConfirmDialog from '@/features/review/components/OutcomeConfirmDialog.vue'
import ShortcutsHelpModal from '@/features/review/components/ShortcutsHelpModal.vue'
import { useReview } from '@/features/review/useReview'
import {
  useReviewShortcuts,
  type NotesFocusHandle,
  type SourceEmailHandle,
} from '@/features/review/useReviewShortcuts'
import type { CandidateHireOutcome, CandidateReviewStatus } from '@/features/candidates/api'
import { candidateFilterStateFromQuery } from '@/features/candidates/filter'
import type { CandidateDetailsPayload } from '@/features/review/api'

const props = defineProps<{
  id: string
  roundId: string
  candidateId: string
}>()

const router = useRouter()
const route = useRoute()
const toast = useToast()
const reviewFilters = computed(() => candidateFilterStateFromQuery(route.query))
const {
  vacancy,
  requirements,
  requirementCount,
  candidate,
  loadError,
  viewState,
  isRoundClosed,
  candidateDetailsWarning,
  conflictWarning,
  position,
  total,
  previousCandidateId,
  nextCandidateId,
  notes,
  notesDirty,
  notesSaveState,
  notesSavedAt,
  savingDetails,
  detailsError,
  savingRequirementId,
  requirementError,
  deciding,
  decisionError,
  settingOutcome,
  outcomeError,
  canSetHireOutcome,
  load,
  saveNotes,
  updateDetails,
  updateRequirementReview,
  toggleRequirementAt,
  setOutcome,
  decide,
  advanceToNextCandidate,
} = useReview(
  toRef(props, 'id'),
  toRef(props, 'roundId'),
  toRef(props, 'candidateId'),
  reviewFilters,
)

const notesEditor = useTemplateRef<NotesFocusHandle>('notesEditor')
const candidateDetailsPanel = useTemplateRef<{ focusEditing: () => void }>('candidateDetailsPanel')
const sourceEmailPanel = useTemplateRef<SourceEmailHandle>('sourceEmailPanel')
const shortcutsHelpOpen = shallowRef(false)
const outcomeDialogOpen = shallowRef(false)
type ConsequentialOutcome = Extract<CandidateHireOutcome, 'runaway' | 'declined'>
const pendingOutcome = shallowRef<ConsequentialOutcome | null>(null)
const announcement = shallowRef('')
const canPrev = computed(() => previousCandidateId.value !== null)
const canNext = computed(() => nextCandidateId.value !== null)
const currentHireOutcome = computed<CandidateHireOutcome>(() => candidate.value?.hireOutcome ?? 'none')

type PendingAnnouncement =
  | { kind: 'decision'; verb: string }
  | { kind: 'navigation' }
  | null

let pendingAnnouncement: PendingAnnouncement = null
let displayedCandidateId: number | null = null

const decisionAnnouncementVerbs: Record<Exclude<CandidateReviewStatus, 'new'>, string> = {
  shortlisted: 'Shortlisted',
  flagged: 'Flagged',
  rejected: 'Rejected',
}

watch(candidateDetailsWarning, (warning) => {
  if (warning) {
    toast.add({
      title: warning,
      color: 'warning',
      class: 'review-details-warning-toast',
    })
  }
})

watch(conflictWarning, (warning) => {
  if (warning) {
    toast.add({
      title: warning.title,
      description: warning.description,
      color: warning.color,
      class: 'review-details-warning-toast',
    })
  }
})

function goToCandidate(id: string | null, nextAnnouncement: PendingAnnouncement = null) {
  if (id === null) {
    return
  }
  pendingAnnouncement = nextAnnouncement
  void router.push({
    name: 'candidate-review',
    params: { id: props.id, roundId: props.roundId, candidateId: id },
    query: route.query,
  })
}

// ADR-0008 #9: Prev/Next navigation silently commits pending notes.
async function onPrev() {
  if (await saveNotes()) {
    goToCandidate(previousCandidateId.value, { kind: 'navigation' })
  }
}

async function onNext() {
  const nextCandidateId = await advanceToNextCandidate()
  goToCandidate(nextCandidateId, nextCandidateId === null ? null : { kind: 'navigation' })
}

// Decision-as-commit: one call saves notes, sets the status, and auto-advances.
async function onDecide(status: CandidateReviewStatus) {
  const result = await decide(status)
  if (result.applied && status === 'shortlisted') {
    toast.add({ title: 'Candidate shortlisted successfully', color: 'success', class: 'review-details-warning-toast' })
  }
  const nextAnnouncement =
    result.nextCandidateId === null
      ? null
      : status === 'new'
        ? { kind: 'navigation' as const }
        : { kind: 'decision' as const, verb: decisionAnnouncementVerbs[status] }
  goToCandidate(
    result.nextCandidateId,
    nextAnnouncement,
  )
}

function onDetailsSave(payload: CandidateDetailsPayload) {
  void updateDetails(payload)
}

function onSetOutcome(outcome: CandidateHireOutcome) {
  if (outcome === 'runaway' || outcome === 'declined') {
    pendingOutcome.value = outcome
    outcomeDialogOpen.value = true
    return
  }
  void setOutcome(outcome)
}

async function onConfirmOutcome(note: string) {
  const outcome = pendingOutcome.value
  if (outcome === null) {
    return
  }

  const nextCandidateAfterOutcome = nextCandidateId.value
  if (!(await setOutcome(outcome, note))) {
    return
  }

  outcomeDialogOpen.value = false
  goToCandidate(
    nextCandidateAfterOutcome,
    nextCandidateAfterOutcome === null ? null : { kind: 'navigation' },
  )
}

function onRequirementToggle(requirementId: number, confirmed: boolean) {
  void updateRequirementReview(requirementId, confirmed)
}

watch(candidate, (current) => {
  if (!current || current.id === displayedCandidateId) {
    return
  }

  const isFirstCandidate = displayedCandidateId === null
  displayedCandidateId = current.id
  if (isFirstCandidate) {
    pendingAnnouncement = null
    return
  }

  const nextAnnouncement = pendingAnnouncement ?? { kind: 'navigation' as const }
  pendingAnnouncement = null
  const prefix = nextAnnouncement.kind === 'decision' ? `${nextAnnouncement.verb}. ` : ''
  announcement.value = `${prefix}Candidate ${position.value} of ${total.value}: ${candidateDisplayName(current)}`
})

useReviewShortcuts({
  canPrev,
  canNext,
  busy: deciding,
  notesEditor,
  sourceEmailPanel,
  shortcutsHelpOpen,
  requirementCount,
  outcomeDialogOpen,
  onEditDetails: () => candidateDetailsPanel.value?.focusEditing(),
  onPrev,
  onNext,
  onDecide,
  canSetOutcome: canSetHireOutcome,
  hireOutcome: currentHireOutcome,
  onSetOutcome,
  onToggleRequirement: (index) => {
    void toggleRequirementAt(index)
  },
  saveNotes,
})

watch(outcomeDialogOpen, (open) => {
  if (!open) {
    pendingOutcome.value = null
  }
})
</script>

<template>
  <div class="flex flex-col gap-5">
    <p class="sr-only" aria-live="polite" aria-atomic="true">{{ announcement }}</p>

    <!-- Shape-matched skeleton (ADR-0008 #14) -->
    <div
      v-if="viewState === 'loading'"
      class="flex flex-col gap-5"
      aria-busy="true"
      aria-label="Loading review workspace"
    >
      <div class="flex items-center gap-3">
        <USkeleton class="size-10 rounded-xl" />
        <div class="min-w-0 flex-1">
          <USkeleton class="h-6 w-1/3" />
          <USkeleton class="mt-2 h-3 w-24" />
        </div>
        <USkeleton class="h-5 w-12" />
      </div>
      <div class="grid gap-5 lg:grid-cols-[minmax(0,2fr)_minmax(0,3fr)]">
        <div class="flex flex-col gap-5">
          <USkeleton class="h-40 w-full rounded-xl" />
          <USkeleton class="h-56 w-full rounded-xl" />
          <USkeleton class="h-24 w-full rounded-xl" />
        </div>
        <USkeleton class="h-[32rem] w-full rounded-xl" />
      </div>
    </div>

    <UAlert
      v-else-if="viewState === 'error'"
      color="error"
      variant="subtle"
      icon="i-lucide-triangle-alert"
      title="Couldn't load the review workspace"
      :description="loadError ?? undefined"
      role="alert"
      :actions="[{ label: 'Try again', color: 'error', variant: 'outline', onClick: load }]"
    />

    <template v-else-if="vacancy && candidate">
      <UAlert
        v-if="isRoundClosed"
        color="neutral"
        variant="subtle"
        icon="i-lucide-lock-keyhole"
        title="This round is closed"
        description="Review data is read-only. Notes, review status, and requirement reviews can no longer be changed."
        class="mb-1"
        data-testid="round-closed-banner"
      />

      <ReviewHeader
        :vacancy-id="id"
        :back-query="route.query"
        :title="vacancy.title"
        :opened-on="vacancy.openedOn"
        :position="position"
        :total="total"
        :processed="vacancy.progress.processedCandidates"
        :progress-total="vacancy.progress.totalCandidates"
      />

      <div class="grid gap-5 lg:grid-cols-[minmax(0,2fr)_minmax(0,3fr)]">
        <!-- Left column scrolls independently; the viewer keeps a fixed height. -->
        <div
          class="flex min-w-0 flex-col gap-5 lg:overflow-y-auto lg:pr-1"
        >
          <RequirementsPanel
            :requirements="requirements"
            :reviews="candidate.requirementReviews"
            :saving-requirement-id="savingRequirementId"
            :error="requirementError"
            @toggle="onRequirementToggle"
          />
          <CandidateDetailsPanel
            ref="candidateDetailsPanel"
            :candidate="candidate"
            :saving="savingDetails"
            :error="detailsError"
            @save="onDetailsSave"
          />
          <SourceEmailPanel
            ref="sourceEmailPanel"
            :subject="candidate.sourceSubject"
            :sender-name="candidate.sourceSenderName"
            :sender-email="candidate.sourceSenderEmail"
            :sent-at="candidate.sourceSentAt"
            :body="candidate.sourceBodyText"
          />
          <NotesEditor
            ref="notesEditor"
            v-model="notes"
            :save-state="notesSaveState"
            :saved-at="notesSavedAt"
            :dirty="notesDirty"
            @save="saveNotes"
          />
        </div>

        <CvViewer :documents="candidate.documents" class="self-start lg:sticky lg:top-6" />
      </div>

      <ReviewActionBar
        :review-status="candidate.reviewStatus"
        :hire-outcome="candidate.hireOutcome"
        :can-set-outcome="canSetHireOutcome"
        :can-prev="previousCandidateId !== null"
        :can-next="nextCandidateId !== null"
        :busy="deciding"
        :error="decisionError"
        :outcome-busy="settingOutcome"
        :outcome-error="outcomeError"
        @prev="onPrev"
        @next="onNext"
        @decide="onDecide"
        @set-outcome="onSetOutcome"
        @help="shortcutsHelpOpen = true"
      />

      <OutcomeConfirmDialog
        v-model:open="outcomeDialogOpen"
        :outcome="pendingOutcome"
        :busy="settingOutcome"
        :error="outcomeError"
        @confirm="onConfirmOutcome"
      />
    </template>

    <ShortcutsHelpModal
      :open="shortcutsHelpOpen"
      @close="shortcutsHelpOpen = false"
    />
  </div>
</template>

<style scoped>
:global(.review-details-warning-toast) {
  position: fixed !important;
  top: 1rem !important;
  right: 1rem !important;
  bottom: auto !important;
  left: auto !important;
  width: min(24rem, calc(100vw - 2rem)) !important;
  z-index: 101 !important;
}
</style>
