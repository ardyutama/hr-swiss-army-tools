<script setup lang="ts">
// Spec-only mount shell: the application root keeps UApp (ADR-0007), and
// UTooltip injects its TooltipProvider from it. Mounting the view bare would
// crash the toolbar toggle and the rule chips. The toaster is off: specs mock
// useToast and assert on the mock, never on rendered toasts.
import VacancyDetailView from './VacancyDetailView.vue'

defineProps<{ id: string }>()
</script>

<template>
  <UApp :toaster="null">
    <VacancyDetailView :id="id" />
  </UApp>
</template>
