<script setup lang="ts">
withDefaults(
  defineProps<{
    variant?: 'primary' | 'ghost' | 'danger' | 'danger-solid'
    loading?: boolean
    disabled?: boolean
  }>(),
  { variant: 'primary', loading: false, disabled: false },
)
</script>

<template>
  <button
    type="button"
    class="btn"
    :class="[`btn--${variant}`, { 'btn--loading': loading }]"
    :disabled="disabled || loading"
  >
    <span v-if="loading" class="btn__spinner" aria-hidden="true" />
    <span class="btn__content"><slot /></span>
  </button>
</template>

<style scoped>
.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  border-radius: var(--radius-sm);
  padding: 0.6rem 1.15rem;
  font-size: var(--text-sm);
  font-weight: 600;
  cursor: pointer;
  border: 1px solid transparent;
  transition: background 0.15s ease, border-color 0.15s ease, color 0.15s ease, opacity 0.15s ease;
  white-space: nowrap;
}

.btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn--primary {
  background: var(--accent);
  color: var(--accent-contrast);
  box-shadow: var(--shadow);
}

.btn--primary:hover:not(:disabled) {
  background: var(--accent-hover);
}

.btn--ghost {
  background: var(--surface);
  border-color: var(--border-strong);
  color: var(--text);
}

.btn--ghost:hover:not(:disabled) {
  background: var(--surface-subtle);
  border-color: var(--muted-soft);
}

.btn--danger {
  background: transparent;
  border-color: var(--border-strong);
  color: var(--danger);
}

.btn--danger:hover:not(:disabled) {
  background: var(--danger-soft);
  border-color: var(--danger);
}

.btn--danger-solid {
  background: var(--danger);
  color: #fff;
  box-shadow: var(--shadow);
}

.btn--danger-solid:hover:not(:disabled) {
  background: var(--danger-hover);
}

.btn__content {
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
}

.btn__spinner {
  width: 0.9rem;
  height: 0.9rem;
  border-radius: 50%;
  border: 2px solid currentColor;
  border-top-color: transparent;
  animation: btn-spin 0.6s linear infinite;
}

@keyframes btn-spin {
  to {
    transform: rotate(360deg);
  }
}
</style>
