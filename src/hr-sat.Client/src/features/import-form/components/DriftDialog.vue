<script setup lang="ts">
import { computed } from 'vue'
import type { ProblemMessageColor } from '@/shared/problem-details'
import type { FormHeaderChangeView } from '../format'

const open = defineModel<boolean>('open', { required: true })

const props = withDefaults(
  defineProps<{
    /** The changed ordinals, enriched with the role and Column Label each carries. */
    changes: FormHeaderChangeView[]
    busy?: boolean
    alert?: { color: ProblemMessageColor; title: string; description?: string } | null
  }>(),
  { busy: false, alert: null },
)

const emit = defineEmits<{
  confirm: []
  remap: []
  cancel: []
}>()

// Decision: a picked ordinal the file no longer has can only be resolved by
// re-mapping — Confirm stays blocked while any change is missing.
const hasMissing = computed(() => props.changes.some((change) => change.missing))

const introLine = computed(() => {
  const count = props.changes.length
  const columns = count === 1 ? '1 column' : `${count} columns`
  return `The file's headers differ from the saved layout at ${columns}. Confirm the mapping still points at the right columns, or re-map first.`
})

function bindingLine(change: FormHeaderChangeView): string {
  const binding = change.role ? `bound to ${change.role}` : 'shown as form answer'
  return change.label
    ? `${change.ordinal} · ${binding} · label “${change.label}”`
    : `${change.ordinal} · ${binding}`
}
</script>

<template>
  <UModal
    v-model:open="open"
    title="Form headers changed"
    :dismissible="false"
    :close="false"
  >
    <template #body>
      <div class="flex flex-col gap-4">
        <UAlert
          color="neutral"
          variant="subtle"
          icon="i-lucide-info"
          :title="introLine"
        />

        <ul class="flex flex-col gap-2" role="list" aria-label="Changed columns">
          <li
            v-for="change in props.changes"
            :key="change.ordinal"
            class="flex flex-col gap-1 rounded-lg border border-default px-3 py-2"
          >
            <p class="text-sm font-medium text-highlighted">{{ bindingLine(change) }}</p>
            <dl class="flex flex-col gap-0.5 text-sm">
              <div class="flex gap-2">
                <dt class="w-10 shrink-0 text-muted">Was:</dt>
                <dd class="min-w-0 break-words text-highlighted">{{ change.was }}</dd>
              </div>
              <div class="flex gap-2">
                <dt class="w-10 shrink-0 text-muted">Now:</dt>
                <dd
                  class="min-w-0 break-words"
                  :class="change.missing ? 'italic text-muted' : 'text-highlighted'"
                >
                  {{ change.now }}
                </dd>
              </div>
            </dl>
          </li>
        </ul>

        <UAlert
          v-if="props.alert"
          :color="props.alert.color"
          variant="subtle"
          icon="i-lucide-triangle-alert"
          :title="props.alert.title"
          :description="props.alert.description"
          role="alert"
        />
      </div>
    </template>

    <template #footer>
      <div class="flex w-full flex-col gap-3">
        <p class="text-sm text-muted" role="status">
          {{
            hasMissing
              ? 'A picked column is no longer in this file — re-map the layout to continue.'
              : 'Confirming keeps the mapping and labels and continues the import.'
          }}
        </p>
        <div class="flex justify-end gap-2">
          <UButton
            color="neutral"
            variant="outline"
            :disabled="props.busy"
            @click="emit('cancel')"
          >
            Cancel import
          </UButton>
          <UButton
            color="neutral"
            variant="soft"
            icon="i-lucide-pencil-line"
            :disabled="props.busy"
            @click="emit('remap')"
          >
            Re-map…
          </UButton>
          <UButton
            color="primary"
            :disabled="hasMissing"
            :loading="props.busy"
            @click="emit('confirm')"
          >
            Confirm mapping & import
          </UButton>
        </div>
      </div>
    </template>
  </UModal>
</template>
