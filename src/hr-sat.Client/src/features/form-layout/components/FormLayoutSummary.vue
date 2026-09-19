<script setup lang="ts">
import { computed } from 'vue'
import { formLayoutRoles, type FormLayoutDto, type FormLayoutRole } from '../api'
import { columnReference, formLayoutRoleNames } from '../format'
import type { FormLayoutViewState } from '../useFormLayout'

const props = withDefaults(
  defineProps<{
    state: FormLayoutViewState
    layout: FormLayoutDto | null
    loadError?: string | null
    canEdit?: boolean
  }>(),
  { loadError: null, canEdit: false },
)

const emit = defineEmits<{
  edit: []
  retry: []
}>()

const answerCount = computed(
  () => props.layout?.columns.filter((column) => column.role === null).length ?? 0,
)
const answersLine = computed(() =>
  answerCount.value === 1 ? '1 column shown' : `${answerCount.value} columns shown`,
)

function roleValue(role: FormLayoutRole): string {
  const column = props.layout?.columns.find((candidate) => candidate.role === role)
  if (!column) {
    return 'Not assigned'
  }
  // The snapshot always names the column; a Column Label never replaces it here.
  return columnReference(column.ordinal, props.layout?.headerSnapshot[column.ordinal] ?? '')
}
</script>

<template>
  <section
    class="overflow-hidden rounded-xl border border-default bg-default shadow-sm"
    aria-label="Form layout"
  >
    <div class="flex items-center justify-between gap-3 border-b border-default px-5 py-3">
      <h2 class="text-xs font-semibold uppercase tracking-[0.06em] text-muted">
        Form layout
      </h2>
      <UButton
        v-if="props.state === 'ready' && props.canEdit"
        color="primary"
        variant="subtle"
        size="sm"
        icon="i-lucide-table-properties"
        @click="emit('edit')"
      >
        Edit layout
      </UButton>
    </div>

    <div
      v-if="props.state === 'loading'"
      class="flex flex-col gap-3 px-5 py-4"
      aria-busy="true"
      aria-label="Loading form layout"
    >
      <USkeleton v-for="n in 4" :key="n" class="h-4 w-2/3" />
    </div>

    <div v-else-if="props.state === 'error'" class="px-5 py-4">
      <UAlert
        color="error"
        variant="subtle"
        icon="i-lucide-triangle-alert"
        title="Couldn't load the form layout"
        :description="props.loadError ?? undefined"
        role="alert"
        :actions="[
          { label: 'Retry', color: 'error', variant: 'outline', onClick: () => emit('retry') },
        ]"
      />
    </div>

    <p v-else-if="props.state === 'empty'" class="px-5 py-4 text-sm text-muted">
      No form layout yet. The first .csv upload walks you through mapping its columns.
    </p>

    <dl v-else-if="props.layout" class="flex flex-col gap-2 px-5 py-4">
      <div
        v-for="role in formLayoutRoles"
        :key="role"
        class="flex items-baseline gap-4 text-sm"
      >
        <dt class="w-32 shrink-0 text-muted">{{ formLayoutRoleNames[role] }}</dt>
        <dd class="min-w-0 truncate text-highlighted">{{ roleValue(role) }}</dd>
      </div>
      <div class="flex items-baseline gap-4 text-sm">
        <dt class="w-32 shrink-0 text-muted">Form answers</dt>
        <dd class="text-highlighted">{{ answersLine }}</dd>
      </div>
    </dl>
  </section>
</template>
