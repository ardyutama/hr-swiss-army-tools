<script setup lang="ts">
import { computed } from 'vue'
import type { PriorApplication } from '../api'
import { formatPriorApplications } from '../format'

const props = defineProps<{
  applications: PriorApplication[]
}>()

const sentence = computed(() => formatPriorApplications(props.applications))
</script>

<template>
  <UAlert
    v-if="sentence"
    data-testid="prior-application-notice"
    color="neutral"
    variant="subtle"
    icon="i-lucide-history"
    title="Prior application"
    :description="sentence"
  />
</template>