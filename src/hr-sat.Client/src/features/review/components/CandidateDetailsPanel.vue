<script setup lang="ts">
import { computed } from 'vue'
import { candidateDisplayName } from '@/features/candidates/format'
import type { CandidateDetails } from '../api'
import { matchedSkillIds } from '../format'

const props = defineProps<{
  candidate: CandidateDetails
  requirementPhrases: string[]
  retrying?: boolean
  retryError?: string | null
}>()

const emit = defineEmits<{
  retry: []
}>()

const matched = computed(() =>
  matchedSkillIds(props.candidate.skills, props.requirementPhrases),
)
</script>

<template>
  <section
    class="rounded-xl border border-default bg-default p-5 shadow-sm"
    aria-label="Candidate details"
  >
    <h2 class="mb-3 text-xs font-semibold uppercase tracking-[0.06em] text-muted">
      Candidate details
    </h2>

    <!-- Extraction pending: shape-matched skeleton rows (ADR-0008 #14). -->
    <div
      v-if="candidate.extractionStatus === 'pending'"
      class="flex flex-col gap-3"
      aria-busy="true"
      aria-label="Extracting candidate details"
    >
      <USkeleton class="h-6 w-1/2" />
      <USkeleton class="h-4 w-2/3" />
      <USkeleton class="h-4 w-1/3" />
      <div class="flex flex-wrap gap-1.5">
        <USkeleton class="h-6 w-16 rounded-full" />
        <USkeleton class="h-6 w-20 rounded-full" />
        <USkeleton class="h-6 w-14 rounded-full" />
      </div>
    </div>

    <!-- Extraction failed: inline error with retry; the PDF stays usable. -->
    <UAlert
      v-else-if="candidate.extractionStatus === 'failed'"
      color="error"
      variant="subtle"
      icon="i-lucide-triangle-alert"
      title="Couldn't extract candidate details"
      :description="retryError ?? 'The details below come from the source email.'"
      role="alert"
      :actions="[
        {
          label: retrying ? 'Retrying…' : 'Retry extraction',
          color: 'error',
          variant: 'outline',
          disabled: retrying,
          onClick: () => emit('retry'),
        },
      ]"
    />

    <template v-else>
      <p class="text-xl font-bold tracking-tight text-highlighted">
        {{ candidateDisplayName(candidate) }}
      </p>
      <dl class="mt-2 flex flex-col gap-1.5">
        <div class="flex items-center gap-2 text-sm text-muted">
          <UIcon name="i-lucide-mail" class="size-4 shrink-0" aria-hidden="true" />
          <dt class="sr-only">Email</dt>
          <dd class="m-0 truncate">{{ candidate.contactEmail ?? '—' }}</dd>
        </div>
        <div class="flex items-center gap-2 text-sm text-muted">
          <UIcon name="i-lucide-phone" class="size-4 shrink-0" aria-hidden="true" />
          <dt class="sr-only">Phone</dt>
          <dd class="m-0">{{ candidate.contactPhone ?? '—' }}</dd>
        </div>
      </dl>

      <div v-if="candidate.skills.length > 0" class="mt-3 flex flex-wrap gap-1.5">
        <span
          v-for="skill in candidate.skills"
          :key="skill.id"
          class="inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium"
          :class="
            matched.has(skill.id)
              ? 'bg-success-soft text-success'
              : 'text-muted ring-1 ring-inset ring-default'
          "
        >
          {{ skill.phrase }}
        </span>
      </div>
      <p v-else class="mt-3 text-sm text-muted">No skills extracted.</p>
    </template>
  </section>
</template>
