<script setup lang="ts">
import { computed } from 'vue'

export type IconName =
  | 'plus'
  | 'pencil'
  | 'trash'
  | 'close'
  | 'briefcase'
  | 'upload'
  | 'arrow-left'
  | 'send'

const props = withDefaults(
  defineProps<{
    name: IconName
    size?: number
  }>(),
  { size: 18 },
)

const paths: Record<IconName, string> = {
  plus: 'M12 5v14M5 12h14',
  pencil:
    'M17 3a2.83 2.83 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5L17 3z',
  trash:
    'M3 6h18M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2m3 0v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6h14zM10 11v6M14 11v6',
  close: 'M18 6 6 18M6 6l12 12',
  briefcase:
    'M20 7H4a2 2 0 0 0-2 2v10a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V9a2 2 0 0 0-2-2zM16 7V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v2',
  upload: 'M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M17 8l-5-5-5 5M12 3v12',
  'arrow-left': 'M19 12H5M12 19l-7-7 7-7',
  send: 'M22 2 11 13M22 2l-7 20-4-9-9-4 22-7z',
}

const d = computed(() => paths[props.name])
</script>

<template>
  <svg
    class="icon"
    :width="size"
    :height="size"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-width="2"
    stroke-linecap="round"
    stroke-linejoin="round"
    aria-hidden="true"
    focusable="false"
  >
    <path :d="d" />
  </svg>
</template>

<style scoped>
.icon {
  display: inline-block;
  flex-shrink: 0;
  vertical-align: middle;
}
</style>
