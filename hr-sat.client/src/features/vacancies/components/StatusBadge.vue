<script setup lang="ts">
defineProps<{
  status: 'Open' | 'Draft' | 'Closed'
}>()
</script>

<template>
  <span class="badge" :class="`badge--${status.toLowerCase()}`">{{ status }}</span>
</template>

<style scoped>
.badge {
  display: inline-block;
  padding: 0.2rem 0.7rem;
  border-radius: 999px;
  font-size: 0.8rem;
  font-weight: 600;
}

.badge--open {
  background: #e5f6ec;
  color: #1c7c43;
}

.badge--draft {
  background: #fdf1dc;
  color: #9a6b1a;
}

.badge--closed {
  background: #eceef1;
  color: var(--muted);
}
</style>
