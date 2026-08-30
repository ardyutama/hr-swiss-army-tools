<script setup lang="ts">
import { useId } from 'vue'

withDefaults(
  defineProps<{
    label: string
    error?: string
    hint?: string
    forId?: string
  }>(),
  { forId: undefined },
)

const autoId = useId()
</script>

<template>
  <div class="field" :class="{ 'field--invalid': !!error }">
    <label class="field__label" :for="forId ?? autoId">{{ label }}</label>
    <slot :id="forId ?? autoId" :invalid="!!error" />
    <p v-if="error" class="field__error" role="alert">{{ error }}</p>
    <p v-else-if="hint" class="field__hint">{{ hint }}</p>
  </div>
</template>

<style scoped>
.field {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
}

.field__label {
  font-size: var(--text-sm);
  font-weight: 600;
  color: var(--text);
}

.field__error {
  margin: 0;
  font-size: var(--text-xs);
  color: var(--danger);
  font-weight: 500;
}

.field__hint {
  margin: 0;
  font-size: var(--text-xs);
  color: var(--muted);
}

/* Shared input styling for any control placed in AppField's default slot. */
.field :deep(input),
.field :deep(textarea),
.field :deep(select) {
  font-family: inherit;
  font-size: var(--text-base);
  color: var(--text);
  background: var(--surface);
  border: 1px solid var(--border-strong);
  border-radius: var(--radius-sm);
  padding: 0.6rem 0.8rem;
  transition: border-color 0.15s ease, box-shadow 0.15s ease;
  width: 100%;
}

.field :deep(input)::placeholder,
.field :deep(textarea)::placeholder {
  color: var(--muted-soft);
}

.field :deep(input):focus,
.field :deep(textarea):focus,
.field :deep(select):focus {
  outline: none;
  border-color: var(--accent);
  box-shadow: var(--focus-ring);
}

.field--invalid :deep(input),
.field--invalid :deep(textarea),
.field--invalid :deep(select) {
  border-color: var(--danger);
}

.field--invalid :deep(input):focus,
.field--invalid :deep(textarea):focus {
  box-shadow: 0 0 0 3px var(--danger-soft);
}
</style>
