<script setup lang="ts">
import AppIcon, { type IconName } from './AppIcon.vue'

withDefaults(
  defineProps<{
    icon: IconName
    label: string
    variant?: 'neutral' | 'danger'
  }>(),
  { variant: 'neutral' },
)
</script>

<template>
  <button
    type="button"
    class="icon-btn"
    :class="`icon-btn--${variant}`"
    :aria-label="label"
    :title="label"
  >
    <AppIcon :name="icon" :size="17" />
  </button>
</template>

<style scoped>
.icon-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 2rem;
  height: 2rem;
  border: 1px solid transparent;
  border-radius: var(--radius-sm);
  background: transparent;
  cursor: pointer;
  transition: background 0.15s ease, color 0.15s ease, border-color 0.15s ease;
}

.icon-btn--neutral {
  color: var(--muted);
}

.icon-btn--neutral:hover {
  background: var(--accent-soft);
  color: var(--accent);
}

.icon-btn--danger {
  color: var(--muted);
}

.icon-btn--danger:hover {
  background: var(--danger-soft);
  color: var(--danger);
}
</style>
