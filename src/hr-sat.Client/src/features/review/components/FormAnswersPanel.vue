<script setup lang="ts">
import type { FormAnswerRow } from '../format'
import type { FormAnswersViewState } from '../useFormAnswers'

// Form Answers takes the review grid's dominant slot for form-sourced
// candidates: the vacancy's picked, role-less columns in column order, rendered
// read-only from the stored raw Form Response (issue 04, grill decision 5).
defineProps<{
  state: Exclude<FormAnswersViewState, 'idle'>
  answers: FormAnswerRow[]
  error: string | null
}>()

const emit = defineEmits<{
  retry: []
}>()
</script>

<template>
  <section class="flex min-w-0 flex-col" aria-label="Form answers">
    <div class="rounded-xl border border-default bg-default shadow-sm">
      <div class="border-b border-default px-4 py-3">
        <h2 class="text-xs font-semibold uppercase tracking-[0.06em] text-muted">
          Form answers
        </h2>
      </div>

      <!-- Shape-matched skeleton while the layout loads (ADR-0008 #14). -->
      <div
        v-if="state === 'loading'"
        class="flex flex-col gap-5 p-4"
        aria-busy="true"
        aria-label="Loading form answers"
      >
        <div v-for="n in 4" :key="n" class="flex flex-col gap-1.5">
          <USkeleton class="h-3 w-28" />
          <USkeleton class="h-4 w-3/4" />
        </div>
      </div>

      <div
        v-else-if="state === 'error'"
        class="flex flex-col items-center gap-3 px-4 py-16 text-center"
        role="alert"
      >
        <UIcon name="i-lucide-file-x-2" class="size-8 text-muted" aria-hidden="true" />
        <p class="m-0 text-sm text-muted">{{ error ?? "Couldn't load the form answers." }}</p>
        <UButton color="neutral" variant="outline" @click="emit('retry')">Try again</UButton>
      </div>

      <p v-else-if="answers.length === 0" class="m-0 px-4 py-16 text-center text-sm text-muted">
        This response has no picked columns to show.
      </p>

      <dl v-else class="flex flex-col divide-y divide-default">
        <div
          v-for="answer in answers"
          :key="answer.ordinal"
          class="flex flex-col gap-1 px-4 py-3"
        >
          <dt class="text-xs font-semibold uppercase tracking-[0.06em] text-muted">
            {{ answer.ordinal }} · {{ answer.label }}<span
              v-if="answer.header"
              class="font-normal normal-case tracking-normal"
            > · {{ answer.header }}</span>
          </dt>
          <!-- Raw cell verbatim: multi-line text wraps, long URLs break. -->
          <dd
            class="m-0 min-w-0 whitespace-pre-wrap break-words text-sm leading-6"
            :class="answer.value.trim() ? 'text-highlighted' : 'text-muted'"
          >{{ answer.value.trim() ? answer.value : '—' }}</dd>
        </div>
      </dl>
    </div>
  </section>
</template>
