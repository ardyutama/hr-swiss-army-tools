<script setup lang="ts">
import { computed, reactive, useTemplateRef, watch } from 'vue'
import type { FormSubmitEvent } from '@nuxt/ui'
import type { ProblemMessageColor } from '@/shared/problem-details'
import type { ScreeningOperator, ScreeningRule, ScreeningRulesPreview } from '../api'
import {
  blankScreeningRule,
  maxScreeningRules,
  operatorNeedsValue,
  screeningRulesFormSchema,
  type ScreeningRulesFormOutput,
} from '../validation'
import { columnOptionLabel } from '../format'

const open = defineModel<boolean>('open', { required: true })

const props = withDefaults(
  defineProps<{
    /** Every column of the header snapshot is offered, picked or not — index IS the ordinal. */
    headerSnapshot: string[]
    /** Column Labels by ordinal, computed by the page from the form layout — screening never imports form-layout. */
    columnLabels: { ordinal: number; label: string | null }[]
    /** The saved rules; the opening draft is rebuilt from them each time the dialog opens. */
    initialRules: ScreeningRule[]
    /** The live preview of the latest announced draft; null while a request is in flight or skipped. */
    preview: ScreeningRulesPreview | null
    /** A failed preview degrades the count line; it never blocks Save. */
    previewFailed?: boolean
    hasActiveRound: boolean
    busy?: boolean
    alert?: { color: ProblemMessageColor; title: string; description?: string } | null
    /** Closed vacancy: inputs disabled, mutations and Save hidden. */
    readonly?: boolean
  }>(),
  { previewFailed: false, busy: false, alert: null, readonly: false },
)

const emit = defineEmits<{
  save: [rules: ScreeningRule[]]
  change: [rules: ScreeningRule[]]
}>()

const form = reactive({ rules: [] as ScreeningRule[] })
// The footer lives outside the UForm body, so submit goes through the form ref.
const formRef = useTemplateRef<{ submit: () => Promise<void> }>('formRef')

// Rebuild the draft each time the dialog opens; it is stateless between
// openings. Default ('pre') flush, never 'sync': open is patched before the
// initial-rules prop in the parent template, and a sync watcher would read it
// stale (the FormLayoutDialog lesson).
watch(open, (isOpen) => {
  if (isOpen) {
    form.rules = props.initialRules.map((rule) => ({ ...rule }))
  }
})

// Every edit announces the draft so the composable can debounce the live
// preview; identical payloads are skipped there, so the opening rebuild is free.
watch(
  () => form.rules,
  (rules) => {
    emit('change', rules.map((rule) => ({ ...rule })))
  },
  { deep: true },
)

interface ColumnOption {
  value: number
  label: string
}

function labelFor(ordinal: number): string | null {
  return props.columnLabels.find((column) => column.ordinal === ordinal)?.label ?? null
}

const snapshotOptions = computed<ColumnOption[]>(() =>
  props.headerSnapshot.map((header, ordinal) => ({
    value: ordinal,
    label: columnOptionLabel(ordinal, header, labelFor(ordinal)),
  })),
)

// A drift-shrunk snapshot can strand a saved rule's ordinal; the orphan stays
// keepable and removable, never a validation block (the server still
// evaluates it — short-row reads as empty).
function columnOptions(rule: ScreeningRule): ColumnOption[] {
  if (rule.ordinal >= 0 && rule.ordinal < props.headerSnapshot.length) {
    return snapshotOptions.value
  }
  return [
    ...snapshotOptions.value,
    {
      value: rule.ordinal,
      label: `Column ${rule.ordinal} — not in the current header snapshot`,
    },
  ]
}

const operatorOptions: { value: ScreeningOperator; label: string }[] = [
  { value: 'equals', label: 'equals' },
  { value: 'not-equals', label: 'not-equals' },
  { value: 'is-empty', label: 'is empty' },
  { value: 'not-empty', label: 'is not empty' },
  { value: 'contains', label: 'contains' },
]

// The empty operators hide and clear the value input; switching back to a
// value operator restores an empty string for the schema to flag until typed.
function onOperatorChange(rule: ScreeningRule, operator: ScreeningOperator | undefined) {
  if (operator === undefined) {
    return
  }
  rule.operator = operator
  if (operatorNeedsValue(operator)) {
    if (rule.value === null) {
      rule.value = ''
    }
  } else {
    rule.value = null
  }
}

const atCap = computed(() => form.rules.length >= maxScreeningRules)
const counterLine = computed(() =>
  atCap.value ? `${maxScreeningRules} rules at most` : `${form.rules.length} of ${maxScreeningRules} rules`,
)

function addRule() {
  if (!atCap.value) {
    form.rules.push(blankScreeningRule())
  }
}

function removeRule(index: number) {
  form.rules.splice(index, 1)
}

// The per-rule amber all/nothing net — the only amber on these surfaces. It is
// suppressed when the round has no form candidates: 0 of 0 is not a signal.
function ruleWarning(index: number): string | null {
  const current = props.preview
  if (current === null || current.total === 0) {
    return null
  }
  const entry = current.perRule.find((candidate) => candidate.index === index)
  if (!entry) {
    return null
  }
  if (entry.screenedOut === 0) {
    return `Rule ${index + 1} matches no one — it will never fire.`
  }
  if (entry.screenedOut === current.total) {
    return `Rule ${index + 1} screens out everyone — check the column.`
  }
  return null
}

const countLine = computed(() => {
  if (!props.hasActiveRound) {
    return 'No active round — saved rules apply to the next round\'s imports.'
  }
  if (props.previewFailed) {
    return 'Live count unavailable'
  }
  const current = props.preview
  if (current === null) {
    return ''
  }
  if (current.total === 0) {
    return 'No form candidates in the active round to screen.'
  }
  return `Would screen out ${current.screenedOut} of ${current.total} form candidates in the active round.`
})

function onSubmit(event: FormSubmitEvent<ScreeningRulesFormOutput>) {
  emit('save', event.data.rules)
}
</script>

<template>
  <UModal
    v-model:open="open"
    title="Screening rules"
    description="Screen out form candidates whose raw answers fail these rules. Rules read the stored CSV text as-is — no number or date parsing — and editing them re-sorts candidates instantly. Screening never deletes anyone."
    :dismissible="!props.busy"
    :ui="{ content: 'sm:max-w-2xl' }"
  >
    <template #body>
      <UForm
        ref="formRef"
        :schema="screeningRulesFormSchema"
        :state="form"
        class="flex flex-col gap-5"
        @submit="onSubmit"
      >
        <UAlert
          v-if="props.alert"
          :color="props.alert.color"
          variant="subtle"
          icon="i-lucide-triangle-alert"
          :title="props.alert.title"
          :description="props.alert.description"
          role="alert"
        />

        <UFormField name="rules">
          <div class="flex flex-col gap-4">
            <fieldset
              v-for="(rule, index) in form.rules"
              :key="index"
              class="flex flex-col gap-3 rounded-lg border border-default p-4"
            >
              <legend class="sr-only">Rule {{ index + 1 }}</legend>
              <div class="flex items-center justify-between gap-3">
                <span class="text-sm font-semibold text-highlighted">Rule {{ index + 1 }}</span>
                <UButton
                  v-if="!props.readonly"
                  color="neutral"
                  variant="ghost"
                  size="sm"
                  icon="i-lucide-trash-2"
                  :aria-label="`Remove rule ${index + 1}`"
                  @click="removeRule(index)"
                >
                  Remove rule
                </UButton>
              </div>

              <UFormField label="Column">
                <USelect
                  v-model="rule.ordinal"
                  :items="columnOptions(rule)"
                  value-key="value"
                  label-key="label"
                  :aria-label="`Rule ${index + 1} column`"
                  :disabled="props.busy || props.readonly"
                  class="w-full"
                />
              </UFormField>

              <div class="grid gap-3 sm:grid-cols-2">
                <UFormField label="Operator">
                  <USelect
                    :model-value="rule.operator"
                    :items="operatorOptions"
                    value-key="value"
                    label-key="label"
                    :aria-label="`Rule ${index + 1} operator`"
                    :disabled="props.busy || props.readonly"
                    class="w-full"
                    @update:model-value="onOperatorChange(rule, $event)"
                  />
                </UFormField>

                <UFormField
                  v-if="operatorNeedsValue(rule.operator)"
                  label="Value"
                  :name="`rules.${index}.value`"
                >
                  <UInput
                    :model-value="rule.value ?? ''"
                    :aria-label="`Rule ${index + 1} value`"
                    :disabled="props.busy || props.readonly"
                    autocomplete="off"
                    class="w-full"
                    @update:model-value="rule.value = String($event ?? '')"
                  />
                </UFormField>
              </div>

              <UAlert
                v-if="ruleWarning(index)"
                color="warning"
                variant="subtle"
                icon="i-lucide-triangle-alert"
                :title="ruleWarning(index) ?? undefined"
              />
            </fieldset>
          </div>
        </UFormField>

        <div v-if="!props.readonly" class="flex items-center justify-between gap-3">
          <UButton
            color="neutral"
            variant="outline"
            icon="i-lucide-plus"
            :disabled="atCap || props.busy"
            @click="addRule"
          >
            Add rule
          </UButton>
          <span class="text-sm tabular-nums text-muted">{{ counterLine }}</span>
        </div>
      </UForm>
    </template>

    <template #footer>
      <div class="flex w-full items-center justify-between gap-4">
        <p class="text-sm text-muted" role="status">{{ countLine }}</p>
        <div class="flex shrink-0 gap-2">
          <UButton
            color="neutral"
            variant="outline"
            :disabled="props.busy"
            @click="open = false"
          >
            Cancel
          </UButton>
          <UButton
            v-if="!props.readonly"
            color="primary"
            :loading="props.busy"
            @click="formRef?.submit()"
          >
            Save rules
          </UButton>
        </div>
      </div>
    </template>
  </UModal>
</template>
