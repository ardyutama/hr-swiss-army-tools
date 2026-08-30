<script setup lang="ts">
defineProps<{
  status: 'open' | 'closed'
}>()
</script>

<template>
  <span class="badge" :class="`badge--${status}`">
    <span class="badge__dot" aria-hidden="true" />
    {{ status === 'open' ? 'Open' : 'Closed' }}
  </span>
</template>

<style scoped>
.badge {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.2rem 0.65rem;
  border-radius: var(--radius-pill);
  font-size: var(--text-xs);
  font-weight: 600;
  letter-spacing: 0.01em;
}

.badge__dot {
  width: 0.45rem;
  height: 0.45rem;
  border-radius: 50%;
  background: currentColor;
}

.badge--open {
  background: var(--success-soft);
  color: var(--success);
}

.badge--closed {
  background: var(--neutral-chip-bg);
  color: var(--neutral-chip-text);
}
</style>
