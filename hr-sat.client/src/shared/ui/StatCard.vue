<script setup lang="ts">
defineProps<{
  label: string
  value: string | number
  hint?: string
}>()
</script>

<template>
  <section class="stat-card">
    <span class="stat-card__label">{{ label }}</span>
    <span class="stat-card__value">{{ value }}</span>
    <span v-if="hint" class="stat-card__hint">{{ hint }}</span>
  </section>
</template>

<style scoped>
.stat-card {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  box-shadow: var(--shadow);
  padding: var(--space-4) var(--space-5);
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  min-width: 11rem;
  flex: 1;
  max-width: 16rem;
}

.stat-card__label {
  color: var(--muted);
  font-size: var(--text-sm);
  font-weight: 500;
}

.stat-card__value {
  font-size: var(--text-2xl);
  font-weight: 700;
  line-height: 1.1;
  letter-spacing: -0.02em;
  color: var(--text);
}

.stat-card__hint {
  color: var(--muted-soft);
  font-size: var(--text-xs);
}
</style>
