<script setup lang="ts">
withDefaults(
  defineProps<{
    variant?: 'primary' | 'ghost' | 'danger'
  }>(),
  { variant: 'primary' },
)
</script>

<template>
  <button type="button" class="btn" :class="`btn--${variant}`">
    <slot />
  </button>
</template>

<style scoped>
.btn {
  border-radius: var(--radius);
  padding: 0.55rem 1.1rem;
  font-size: 0.9rem;
  font-weight: 600;
  cursor: pointer;
  border: 1px solid transparent;
  transition: background 0.15s ease, border-color 0.15s ease;
}

.btn--primary {
  background: var(--accent);
  color: #fff;
  box-shadow: var(--shadow);
}

.btn--primary:hover {
  background: var(--accent-hover);
}

.btn--ghost {
  background: transparent;
  border-color: var(--border);
  color: var(--accent);
  padding: 0.35rem 0.8rem;
}

.btn--ghost:hover {
  background: var(--accent-soft);
  border-color: var(--accent);
}

.btn--danger {
  background: transparent;
  border-color: var(--border);
  color: var(--danger);
  padding: 0.35rem 0.8rem;
}

.btn--danger:hover {
  border-color: var(--danger);
}
</style>
