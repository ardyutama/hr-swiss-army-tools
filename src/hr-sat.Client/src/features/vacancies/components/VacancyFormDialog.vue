<script setup lang="ts">
import { computed, reactive, shallowRef, useTemplateRef, watch } from 'vue'
import type { FormSubmitEvent } from '@nuxt/ui'
import { fieldErrorsOf, formErrorsOnly } from '@/shared/validation'
import type { VacancyDetails, VacancySummary, VacancyWritePayload } from '../api'
import {
  vacancyFormFieldKeys,
  vacancyFormSchema,
  vacancyRequirementValues,
  vacancyServerFieldNames,
  type VacancyFormOutput,
  type VacancyRequirementRow,
} from '../validation'

const props = defineProps<{
  open: boolean
  vacancy: VacancySummary | null
  details: VacancyDetails | null
  saving: boolean
}>()

const emit = defineEmits<{
  close: []
  submit: [payload: VacancyWritePayload]
}>()

const isEdit = computed(() => props.vacancy !== null)
const dialogTitle = computed(() => (isEdit.value ? 'Edit vacancy' : 'Add vacancy'))

let nextRequirementId = 0

function requirementRow(value = ''): VacancyRequirementRow {
  return { id: nextRequirementId++, value }
}

const form = reactive({
  title: '',
  openedOn: '',
  neededHires: undefined as number | '' | undefined,
  requirements: [requirementRow()] as VacancyRequirementRow[],
})

const formRef = useTemplateRef('formRef')
const initialFormPayload = shallowRef<VacancyWritePayload | null>(null)

const isDirty = computed(() => {
  const initialPayload = initialFormPayload.value
  return initialPayload !== null && !vacancyPayloadsEqual(currentFormPayload(), initialPayload)
})

const saveStatusLabel = computed(() => {
  if (props.saving) {
    return 'Saving...'
  }
  if (isDirty.value) {
    return 'Unsaved changes'
  }
  return isEdit.value ? 'Saved' : null
})

const saveStatusIcon = computed(() => {
  if (props.saving) {
    return 'i-lucide-loader-circle'
  }
  return isDirty.value ? 'i-lucide-pencil-line' : 'i-lucide-circle-check'
})

const saveStatusClass = computed(() => {
  if (props.saving) {
    return 'text-muted'
  }
  return isDirty.value ? 'text-primary' : 'text-success'
})

const neededHiresLabel = computed(() => {
  const neededHires = normalizedNeededHires(form.neededHires)
  if (neededHires === null) {
    return 'Not set'
  }
  return `${neededHires} ${neededHires === 1 ? 'person' : 'people'}`
})

function resetForm() {
  form.title = props.vacancy?.title ?? ''
  form.openedOn = props.vacancy?.openedOn ?? todayIso()
  form.neededHires = props.vacancy?.hiring?.neededHires
  form.requirements = [requirementRow()]
  initialFormPayload.value = currentFormPayload()
}

function payloadFromOutput(output: VacancyFormOutput): VacancyWritePayload {
  return {
    title: output.title.trim(),
    openedOn: output.openedOn,
    neededHires: output.neededHires,
    requirements: vacancyRequirementValues(output.requirements.map((row) => row.value)),
  }
}

function currentFormPayload(): VacancyWritePayload {
  return {
    title: form.title.trim(),
    openedOn: form.openedOn,
    neededHires: normalizedNeededHires(form.neededHires),
    requirements: vacancyRequirementValues(form.requirements.map((row) => row.value)),
  }
}

function normalizedNeededHires(value: number | '' | undefined): number | null {
  return value === '' || value === undefined ? null : value
}

function vacancyPayloadsEqual(
  left: VacancyWritePayload,
  right: VacancyWritePayload,
): boolean {
  return (
    left.title === right.title &&
    left.openedOn === right.openedOn &&
    left.neededHires === right.neededHires &&
    left.requirements.length === right.requirements.length &&
    left.requirements.every((requirement, index) => requirement === right.requirements[index])
  )
}

watch(
  () => props.open,
  (isOpen) => {
    if (isOpen) {
      resetForm()
    }
  },
)

// Apply the full prefill once the vacancy details arrive from the composable.
watch(
  () => props.details,
  (details) => {
    if (!props.open || !details || details.id !== props.vacancy?.id) {
      return
    }
    const requirements = [...details.requirements].sort((a, b) => a.position - b.position)
    form.title = details.title
    form.openedOn = details.openedOn
    form.neededHires = details.hiring?.neededHires
    form.requirements =
      requirements.length > 0
        ? requirements.map((r) => requirementRow(r.phrase))
        : [requirementRow()]
    initialFormPayload.value = {
      title: details.title,
      openedOn: details.openedOn,
      neededHires: details.hiring?.neededHires ?? null,
      requirements: requirements.map((requirement) => requirement.phrase),
    }
  },
)

function todayIso(): string {
  const now = new Date()
  const month = String(now.getMonth() + 1).padStart(2, '0')
  const day = String(now.getDate()).padStart(2, '0')
  return `${now.getFullYear()}-${month}-${day}`
}

function addRequirement() {
  form.requirements.push(requirementRow())
}

function removeRequirement(index: number) {
  form.requirements.splice(index, 1)
}

function clearNeededHires() {
  form.neededHires = undefined
}

function onSubmit(event: FormSubmitEvent<VacancyFormOutput>) {
  const payload = payloadFromOutput(event.data)
  if (
    isEdit.value &&
    props.vacancy &&
    !isDirty.value
  ) {
    emit('close')
    return
  }
  emit('submit', payload)
}

defineExpose({
  markSaved() {
    initialFormPayload.value = currentFormPayload()
  },
  /** Routes a failed save's ValidationProblem back onto the fields it names. */
  applyServerErrors(error: unknown) {
    const serverErrors = fieldErrorsOf(error)
    if (!serverErrors) {
      return
    }
    const formErrors = formErrorsOnly(serverErrors, vacancyFormFieldKeys)
    formRef.value?.setErrors(
      Object.entries(formErrors).flatMap(([key, messages]) =>
        messages.map((message) => ({
          name: vacancyServerFieldNames[key] ?? key,
          message,
        })),
      ),
    )
  },
})
</script>

<template>
  <UModal
    :open="open"
    :title="dialogTitle"
    :dismissible="!saving"
    @update:open="(value) => { if (!value) emit('close') }"
  >
    <template #body>
      <UForm
        ref="formRef"
        :schema="vacancyFormSchema"
        :state="form"
        class="flex flex-col gap-5"
        @submit="onSubmit"
      >
        <UFormField label="Vacancy title" name="title">
          <UInput
            v-model="form.title"
            placeholder="e.g. Senior Welder"
            maxlength="200"
            autocomplete="off"
            class="w-full"
          />
        </UFormField>

        <UFormField label="Opening date" name="openedOn">
          <UInput v-model="form.openedOn" type="date" class="w-full" />
        </UFormField>

        <UFormField
          label="Hiring target"
          name="neededHires"
        >
          <div class="flex flex-col gap-3 rounded-xl border border-default bg-muted/30 p-3">
            <div class="flex items-start justify-between gap-3">
              <div class="min-w-0">
                <p class="text-sm font-medium text-highlighted">Needed hires</p>
                <p class="text-xs text-muted">Optional target for this vacancy.</p>
              </div>
              <span class="shrink-0 text-xs font-medium text-muted">{{ neededHiresLabel }}</span>
            </div>
            <div class="flex items-center gap-2">
              <UInput
                v-model.number="form.neededHires"
                type="number"
                min="1"
                max="9999"
                step="1"
                placeholder="e.g. 10"
                aria-label="Needed hires"
                class="w-32"
              />
              <span class="text-sm text-muted">people</span>
              <UButton
                v-if="normalizedNeededHires(form.neededHires) !== null"
                icon="i-lucide-x"
                color="neutral"
                variant="ghost"
                size="sm"
                aria-label="Clear needed hires"
                title="Clear needed hires"
                @click="clearNeededHires"
              />
            </div>
          </div>
        </UFormField>

        <UFormField
          label="Skill requirements"
          name="requirements"
          hint="CVs are sorted against these. Add at least one."
        >
          <div class="flex flex-col gap-2 w-full">
            <div
              v-for="(row, index) in form.requirements"
              :key="row.id"
              class="flex items-center gap-2"
            >
              <UInput
                v-model="row.value"
                :placeholder="`Requirement ${index + 1}`"
                maxlength="200"
                autocomplete="off"
                :aria-label="`Requirement ${index + 1}`"
                class="flex-1"
              />
              <UButton
                icon="i-lucide-x"
                color="neutral"
                variant="ghost"
                :aria-label="`Remove requirement ${index + 1}`"
                :disabled="form.requirements.length === 1"
                @click="removeRequirement(index)"
              />
            </div>
            <UButton
              icon="i-lucide-plus"
              color="primary"
              variant="outline"
              class="self-start border-dashed"
              @click="addRequirement"
            >
              Add requirement
            </UButton>
          </div>
        </UFormField>
      </UForm>
    </template>

    <template #footer>
      <div class="flex flex-wrap items-center justify-between gap-3">
        <p
          v-if="saveStatusLabel"
          class="flex items-center gap-1.5 text-xs font-medium"
          :class="saveStatusClass"
          role="status"
          aria-live="polite"
        >
          <UIcon :name="saveStatusIcon" class="size-4" :class="{ 'animate-spin': saving }" />
          <span>{{ saveStatusLabel }}</span>
        </p>
        <UButton color="neutral" variant="outline" :disabled="saving" @click="emit('close')">
          Cancel
        </UButton>
        <div class="flex gap-2">
          <UButton
            :loading="saving"
            :disabled="isEdit && !isDirty"
            @click="formRef?.submit()"
          >
            {{ isEdit ? 'Save changes' : 'Create vacancy' }}
          </UButton>
        </div>
      </div>
    </template>
  </UModal>
</template>
