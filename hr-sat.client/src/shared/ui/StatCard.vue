<script setup lang="ts">
defineProps<{
  label: string
  value: string | number
  hint?: string
}>()
</script>

<template>
  <section class="stat-card">
    <span class="stat-card__value">{{ value }}</span>
    <span class="stat-card__label">{{ label }}</span>
    <span v-if="hint" class="stat-card__hint">{{ hint }}</span>
  </section>
</template>

<style scoped>
.stat-card {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  box-shadow: var(--shadow);
  padding: 1rem 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  min-width: 180px;
}

.stat-card__value {
  font-size: 1.75rem;
  font-weight: 700;
  line-height: 1.1;
  color: var(--accent);
}

.stat-card__label {
  color: var(--muted);
  font-size: 0.875rem;
}

.stat-card__hint {
  color: var(--muted);
  font-size: 0.75rem;
  font-style: italic;
}
</style>
