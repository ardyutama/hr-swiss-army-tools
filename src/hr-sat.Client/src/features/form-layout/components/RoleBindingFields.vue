<script setup lang="ts">
import { computed } from 'vue'
import { formLayoutRoles, type FormLayoutRole } from '../api'
import { columnReference, formLayoutRoleNames, truncateHeader } from '../format'
import { maxColumnLabelLength, maxPickedColumns, type FormLayoutRow } from '../validation'

const props = withDefaults(
  defineProps<{
    rows: FormLayoutRow[]
    busy?: boolean
  }>(),
  { busy: false },
)

const emit = defineEmits<{
  bind: [role: FormLayoutRole, ordinal: number | null]
  updateLabel: [ordinal: number, label: string]
}>()

const requiredRoles: ReadonlySet<FormLayoutRole> = new Set(['name', 'contactEmail'])

const pickedCount = computed(
  () => props.rows.filter((row) => row.role !== null || row.display).length,
)

function boundRow(role: FormLayoutRole): FormLayoutRow | null {
  return props.rows.find((row) => row.role === role) ?? null
}

interface RoleOption {
  label: string
  value: number | null
  disabled?: boolean
}

function roleItems(role: FormLayoutRole): RoleOption[] {
  const items: RoleOption[] = [{ label: 'Not assigned', value: null }]
  for (const row of props.rows) {
    // A column binds to at most one role; binding a fresh column at the cap
    // would push the vacancy past 8 picked columns.
    const boundElsewhere = row.role !== null && row.role !== role
    const overCap =
      pickedCount.value >= maxPickedColumns && row.role === null && !row.display
    items.push({
      label: row.present
        ? columnReference(row.ordinal, row.header)
        : `${columnReference(row.ordinal, row.header)} (no longer in this file)`,
      value: row.ordinal,
      disabled: boundElsewhere || overCap,
    })
  }
  return items
}

function onBindingChange(role: FormLayoutRole, value: number | null | undefined) {
  emit('bind', role, typeof value === 'number' ? value : null)
}
</script>

<template>
  <fieldset class="flex flex-col gap-4">
    <legend class="sr-only">Roles</legend>
    <p class="text-sm text-muted">
      Each role binds to at most one column and pre-fills Candidate Details.
    </p>

    <div v-for="role in formLayoutRoles" :key="role" class="flex flex-col gap-1.5">
      <label
        class="text-sm font-medium text-highlighted"
        :for="`form-layout-role-${role}`"
      >
        {{ formLayoutRoleNames[role] }}
        <span v-if="requiredRoles.has(role)" aria-hidden="true" class="text-error">*</span>
      </label>
      <USelect
        :id="`form-layout-role-${role}`"
        :model-value="boundRow(role)?.ordinal ?? null"
        :items="roleItems(role)"
        value-key="value"
        label-key="label"
        :aria-label="formLayoutRoleNames[role]"
        :disabled="props.busy"
        class="w-full"
        @update:model-value="onBindingChange(role, $event)"
      />
      <template v-if="boundRow(role)">
        <div class="flex items-center gap-2">
          <label class="sr-only" :for="`form-layout-label-${role}`">
            Short label for {{ formLayoutRoleNames[role] }}
          </label>
          <UInput
            :id="`form-layout-label-${role}`"
            :model-value="boundRow(role)!.label"
            :placeholder="`Short label — ${truncateHeader(boundRow(role)!.header)}`"
            :maxlength="maxColumnLabelLength"
            :disabled="props.busy"
            autocomplete="off"
            class="w-full"
            @update:model-value="emit('updateLabel', boundRow(role)!.ordinal, String($event ?? ''))"
          />
          <span class="shrink-0 text-xs text-muted tabular-nums" aria-hidden="true">
            {{ boundRow(role)!.label.length }}/{{ maxColumnLabelLength }}
          </span>
        </div>
        <p class="text-xs text-muted">Header: {{ boundRow(role)!.header }}</p>
      </template>
    </div>
  </fieldset>
</template>
