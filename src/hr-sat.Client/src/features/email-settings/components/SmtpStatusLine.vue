<script setup lang="ts">
import { computed } from 'vue'
import type { SmtpSettings } from '../api'
import { smtpStatusLine } from '../format'

const props = defineProps<{ settings: SmtpSettings }>()

const line = computed(() => smtpStatusLine(props.settings))
</script>

<template>
  <p class="text-sm text-muted">{{ line }}</p>
</template>
