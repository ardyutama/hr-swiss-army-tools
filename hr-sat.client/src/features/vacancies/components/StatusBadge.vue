<script setup lang="ts">
defineProps<{
  status: 'open' | 'closed'
}>()
</script>

<template>
  <UBadge
    :color="status === 'open' ? 'success' : 'neutral'"
    variant="subtle"
    class="gap-1.5"
  >
    <span class="size-1.5 rounded-full bg-current" aria-hidden="true" />
    {{ status === 'open' ? 'Open' : 'Closed' }}
  </UBadge>
</template>
