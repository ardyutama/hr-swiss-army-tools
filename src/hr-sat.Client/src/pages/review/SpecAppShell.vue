<script setup lang="ts">
// Spec-only mount shell: the application root keeps UApp (ADR-0007), and
// UTooltip injects its TooltipProvider from it. Mounting the view bare would
// crash the review header's Resubmitted tooltip. The toaster is off: specs mock
// useToast and assert on the mock, never on rendered toasts.
</script>

<template>
  <UApp :toaster="null">
    <RouterView />
  </UApp>
</template>
