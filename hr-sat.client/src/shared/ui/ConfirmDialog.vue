<script setup lang="ts">
import AppButton from './AppButton.vue'
import AppDialog from './AppDialog.vue'

withDefaults(
  defineProps<{
    open: boolean
    title: string
    confirmLabel: string
    confirming?: boolean
    error?: string | null
  }>(),
  { confirming: false, error: null },
)

const emit = defineEmits<{
  close: []
  confirm: []
}>()
</script>

<template>
  <AppDialog :open="open" :title="title" @close="emit('close')">
    <div class="confirm">
      <slot />
      <p v-if="error" class="confirm__error" role="alert">{{ error }}</p>
    </div>

    <template #footer>
      <AppButton variant="ghost" :disabled="confirming" @click="emit('close')">Cancel</AppButton>
      <AppButton variant="danger-solid" :loading="confirming" @click="emit('confirm')">
        {{ confirmLabel }}
      </AppButton>
    </template>
  </AppDialog>
</template>

<style scoped>
.confirm {
  display: flex;
  flex-direction: column;
}

.confirm__error {
  margin: var(--space-3) 0 0;
  padding: var(--space-3);
  border-radius: var(--radius-sm);
  background: var(--danger-soft);
  color: var(--danger);
  font-size: var(--text-sm);
  font-weight: 500;
}
</style>
