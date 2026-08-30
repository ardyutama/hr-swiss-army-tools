<script setup lang="ts">
import { onBeforeUnmount, useTemplateRef, watch } from 'vue'
import AppIcon from './AppIcon.vue'

const props = defineProps<{
  open: boolean
  title: string
}>()

const emit = defineEmits<{
  close: []
}>()

const panel = useTemplateRef<HTMLElement>('panel')
let previouslyFocused: HTMLElement | null = null

const FOCUSABLE =
  'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])'

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') {
    event.stopPropagation()
    emit('close')
    return
  }
  if (event.key !== 'Tab' || !panel.value) {
    return
  }
  const focusables = Array.from(panel.value.querySelectorAll<HTMLElement>(FOCUSABLE)).filter(
    (el) => el.offsetParent !== null,
  )
  if (focusables.length === 0) {
    event.preventDefault()
    return
  }
  const first = focusables[0]!
  const last = focusables[focusables.length - 1]!
  const active = document.activeElement
  if (event.shiftKey && active === first) {
    event.preventDefault()
    last.focus()
  } else if (!event.shiftKey && active === last) {
    event.preventDefault()
    first.focus()
  }
}

watch(
  () => props.open,
  (isOpen) => {
    if (isOpen) {
      previouslyFocused = document.activeElement as HTMLElement | null
      document.addEventListener('keydown', onKeydown, true)
      document.body.style.overflow = 'hidden'
      requestAnimationFrame(() => {
        const focusables = panel.value?.querySelectorAll<HTMLElement>(FOCUSABLE)
        const target = focusables && focusables.length > 0 ? focusables[0] : panel.value
        target?.focus()
      })
    } else {
      document.removeEventListener('keydown', onKeydown, true)
      document.body.style.overflow = ''
      previouslyFocused?.focus()
      previouslyFocused = null
    }
  },
  { immediate: true },
)

onBeforeUnmount(() => {
  document.removeEventListener('keydown', onKeydown, true)
  document.body.style.overflow = ''
})
</script>

<template>
  <Teleport to="body">
    <Transition name="dialog">
      <div v-if="open" class="dialog__overlay" @click.self="emit('close')">
        <div
          ref="panel"
          class="dialog__panel"
          role="dialog"
          aria-modal="true"
          :aria-label="title"
          tabindex="-1"
        >
          <header class="dialog__header">
            <h2 class="dialog__title">{{ title }}</h2>
            <button type="button" class="dialog__close" aria-label="Close" @click="emit('close')">
              <AppIcon name="close" :size="20" />
            </button>
          </header>
          <div class="dialog__body">
            <slot />
          </div>
          <footer v-if="$slots.footer" class="dialog__footer">
            <slot name="footer" />
          </footer>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.dialog__overlay {
  position: fixed;
  inset: 0;
  background: rgb(24 28 38 / 0.45);
  backdrop-filter: blur(2px);
  display: flex;
  align-items: flex-start;
  justify-content: center;
  padding: var(--space-10) var(--space-4);
  z-index: 50;
  overflow-y: auto;
}

.dialog__panel {
  background: var(--surface);
  border-radius: var(--radius);
  box-shadow: var(--shadow-lg);
  width: 100%;
  max-width: 30rem;
  display: flex;
  flex-direction: column;
  outline: none;
}

.dialog__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-4);
  padding: var(--space-5) var(--space-6) var(--space-4);
  border-bottom: 1px solid var(--border);
}

.dialog__title {
  font-size: var(--text-lg);
  font-weight: 600;
}

.dialog__close {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 2rem;
  height: 2rem;
  border: none;
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--muted);
  cursor: pointer;
  transition: background 0.15s ease, color 0.15s ease;
}

.dialog__close:hover {
  background: var(--surface-subtle);
  color: var(--text);
}

.dialog__body {
  padding: var(--space-5) var(--space-6);
}

.dialog__footer {
  display: flex;
  justify-content: flex-end;
  gap: var(--space-3);
  padding: var(--space-4) var(--space-6) var(--space-5);
  border-top: 1px solid var(--border);
}

.dialog-enter-active,
.dialog-leave-active {
  transition: opacity 0.18s ease;
}

.dialog-enter-active .dialog__panel,
.dialog-leave-active .dialog__panel {
  transition: transform 0.18s ease, opacity 0.18s ease;
}

.dialog-enter-from,
.dialog-leave-to {
  opacity: 0;
}

.dialog-enter-from .dialog__panel,
.dialog-leave-to .dialog__panel {
  transform: translateY(8px) scale(0.98);
  opacity: 0;
}
</style>
