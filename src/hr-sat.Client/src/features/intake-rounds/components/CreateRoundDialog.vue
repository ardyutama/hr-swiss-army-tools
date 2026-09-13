<script setup lang="ts">
import { shallowRef, watch } from 'vue'

const open = defineModel<boolean>('open', { required: true })

const props = withDefaults(
  defineProps<{
    saving?: boolean
  }>(),
  { saving: false },
)

const emit = defineEmits<{
  close: []
  submit: [payload: { name: string | null }]
}>()

const name = shallowRef('')

// Reset the draft each time the dialog opens.
watch(
  open,
  (open) => {
    if (open) {
      name.value = ''
    }
  },
)

function submit() {
  const trimmed = name.value.trim()
  emit('submit', { name: trimmed === '' ? null : trimmed })
}
</script>

<template>
  <UModal
    v-model:open="open"
    title="Open a new round"
    :dismissible="!props.saving"
  >
    <template #body>
      <form class="flex flex-col gap-3" @submit.prevent="submit">
        <label class="flex flex-col gap-1.5 text-sm">
          <span class="font-medium text-highlighted">Round name <span class="text-muted">(optional)</span></span>
          <input
            v-model="name"
            type="text"
            maxlength="200"
            placeholder="e.g. Second wave"
            aria-label="Round name"
            class="min-h-10 rounded-xl border border-default bg-default px-3 py-2 text-sm text-highlighted outline-none focus:border-primary"
          />
        </label>
        <p class="text-sm text-muted">
          Opening a round makes it the active round. New imports land in the active round.
        </p>
      </form>
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton color="neutral" variant="outline" :disabled="props.saving" @click="open = false">
          Cancel
        </UButton>
        <UButton color="primary" :loading="props.saving" @click="submit">
          Open round
        </UButton>
      </div>
    </template>
  </UModal>
</template>
