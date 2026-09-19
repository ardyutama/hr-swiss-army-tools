<script setup lang="ts">
import { computed } from 'vue'
import { columnReference, pickedCounterText, truncateHeader } from '../format'
import { maxColumnLabelLength, maxPickedColumns, type FormLayoutRow } from '../validation'

const props = withDefaults(
  defineProps<{
    rows: FormLayoutRow[]
    busy?: boolean
  }>(),
  { busy: false },
)

const emit = defineEmits<{
  toggle: [ordinal: number, checked: boolean]
  updateLabel: [ordinal: number, label: string]
}>()

// A column is either a role or a display answer, never both: role-bound
// columns leave this list (and come back unchecked when un-bound).
const candidates = computed(() => props.rows.filter((row) => row.role === null))

const pickedCount = computed(
  () => props.rows.filter((row) => row.role !== null || row.display).length,
)

const counter = computed(() => pickedCounterText(pickedCount.value))
const atCap = computed(() => pickedCount.value >= maxPickedColumns)

function blocked(row: FormLayoutRow): boolean {
  return !row.display && atCap.value
}
</script>

<template>
  <section class="flex flex-col gap-3" aria-label="Form answers">
    <div class="flex items-baseline justify-between gap-4">
      <div>
        <h3 class="text-sm font-medium text-highlighted">Form answers on the review page</h3>
        <p class="text-xs text-muted">
          Display-only, shown in column order. Roles count toward the {{ maxPickedColumns }}.
        </p>
      </div>
      <span
        class="shrink-0 text-xs font-medium tabular-nums"
        :class="atCap ? 'text-highlighted' : 'text-muted'"
      >
        {{ counter }}
      </span>
    </div>

    <fieldset class="flex flex-col gap-2">
      <legend class="sr-only">Form answers</legend>
      <div
        v-for="row in candidates"
        :key="row.ordinal"
        class="flex flex-col gap-1.5 rounded-lg border border-default px-3 py-2"
      >
        <label class="flex items-center gap-2 text-sm">
          <UCheckbox
            :model-value="row.display"
            :disabled="props.busy || blocked(row)"
            :aria-label="columnReference(row.ordinal, row.header)"
            @update:model-value="emit('toggle', row.ordinal, $event === true)"
          />
          <span class="min-w-0 truncate text-highlighted">
            {{ columnReference(row.ordinal, row.header) }}
          </span>
          <span v-if="!row.present" class="shrink-0 text-xs text-muted">
            (no longer in this file)
          </span>
        </label>
        <div v-if="row.display" class="flex items-center gap-2 pl-7">
          <label class="sr-only" :for="`form-layout-label-${row.ordinal}`">
            Short label for column {{ row.ordinal }}
          </label>
          <UInput
            :id="`form-layout-label-${row.ordinal}`"
            :model-value="row.label"
            :placeholder="`Short label — ${truncateHeader(row.header)}`"
            :maxlength="maxColumnLabelLength"
            :disabled="props.busy"
            autocomplete="off"
            class="w-full"
            @update:model-value="emit('updateLabel', row.ordinal, String($event ?? ''))"
          />
          <span class="shrink-0 text-xs text-muted tabular-nums" aria-hidden="true">
            {{ row.label.length }}/{{ maxColumnLabelLength }}
          </span>
        </div>
      </div>
    </fieldset>

    <p class="text-xs text-muted">
      Timestamp (column 0) is imported automatically and never picked.
    </p>
  </section>
</template>
