<script setup lang="ts">
interface ShortcutRow {
  key: string
  action: string
  context: string
}

const shortcutRows: ShortcutRow[] = [
  { key: '←', action: 'Previous candidate', context: 'Triage mode' },
  { key: '→', action: 'Next candidate', context: 'Triage mode' },
  { key: 'Shift+← / Shift+→', action: 'Previous/next CV page', context: 'Review workspace' },
  { key: 'S', action: 'Shortlist candidate', context: 'Triage mode' },
  { key: 'F', action: 'Flag candidate', context: 'Triage mode' },
  { key: 'R', action: 'Reject candidate', context: 'Triage mode' },
  { key: '1-9', action: 'Toggle requirement by position', context: 'Triage mode' },
  { key: 'E', action: 'Edit candidate details', context: 'Triage mode' },
  { key: 'O', action: 'Open/close source email', context: 'Triage mode' },
  { key: 'N', action: 'Focus Notes', context: 'Triage mode' },
  { key: 'N', action: 'Leave Notes and save', context: 'Editing mode, Notes focused' },
  { key: 'Esc', action: 'Leave editing and save Notes', context: 'Editing mode' },
  { key: '?', action: 'Open shortcuts help', context: 'Triage mode' },
]

const props = defineProps<{
  open: boolean
}>()

const emit = defineEmits<{
  close: []
}>()
</script>

<template>
  <UModal
    :open="props.open"
    title="Keyboard shortcuts"
    description="Keep candidate review moving without leaving the keyboard."
    :ui="{ content: 'sm:max-w-2xl' }"
    @update:open="(value) => { if (!value) emit('close') }"
  >
    <template #body>
      <div class="flex flex-col gap-4">
        <p class="m-0 text-sm leading-6 text-muted">
          Triage mode arms navigation and decision shortcuts. Editing mode keeps Notes and Escape
          available while text fields have focus.
        </p>
        <div class="overflow-x-auto rounded-xl border border-default">
          <table class="w-full min-w-[32rem] text-left text-sm">
            <caption class="sr-only">Review workspace keyboard shortcuts</caption>
            <thead class="border-b border-default bg-muted/40 text-xs uppercase tracking-[0.06em] text-muted">
              <tr>
                <th scope="col" class="px-4 py-3 font-semibold">Key</th>
                <th scope="col" class="px-4 py-3 font-semibold">Action</th>
                <th scope="col" class="px-4 py-3 font-semibold">Context</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-default">
              <tr v-for="shortcut in shortcutRows" :key="`${shortcut.key}-${shortcut.action}`">
                <th scope="row" class="whitespace-nowrap px-4 py-3 font-medium text-highlighted">
                  <kbd class="rounded border border-default bg-muted/50 px-1.5 py-0.5 text-xs font-semibold">
                    {{ shortcut.key }}
                  </kbd>
                </th>
                <td class="px-4 py-3 text-highlighted">{{ shortcut.action }}</td>
                <td class="px-4 py-3 text-muted">{{ shortcut.context }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </template>

    <template #footer>
      <div class="flex justify-end">
        <UButton color="neutral" variant="outline" @click="emit('close')">Close</UButton>
      </div>
    </template>
  </UModal>
</template>