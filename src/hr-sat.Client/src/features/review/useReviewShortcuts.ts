import type { CandidateReviewStatus } from '@/features/candidates/api'
import { computed, type Ref } from 'vue'

export const requirementShortcutKeys = ['1', '2', '3', '4', '5', '6', '7', '8', '9'] as const

export interface NotesFocusHandle {
  focus: () => void
  blur: () => void
  isFocused: () => boolean
}

export interface SourceEmailHandle {
  open: () => void
  toggle: () => void
  close: () => void
  isOpen: () => boolean
}

interface UseReviewShortcutsOptions {
  canPrev: Readonly<Ref<boolean>>
  canNext: Readonly<Ref<boolean>>
  busy: Readonly<Ref<boolean>>
  notesEditor: Readonly<Ref<NotesFocusHandle | null>>
  sourceEmailPanel: Readonly<Ref<SourceEmailHandle | null>>
  shortcutsHelpOpen: Ref<boolean>
  requirementCount: Readonly<Ref<number>>
  onPrev: () => void | Promise<void>
  onNext: () => void | Promise<void>
  onEditDetails: () => void
  onDecide: (status: Exclude<CandidateReviewStatus, 'new'>) => void | Promise<void>
  onToggleRequirement: (index: number) => void | Promise<void>
  saveNotes: () => Promise<boolean>
}

type ShortcutDefinition =
  | ((event?: KeyboardEvent) => void)
  | {
      usingInput?: string | boolean
      handler: (event?: KeyboardEvent) => void
    }

function isEditableElement(element: Element | null): element is HTMLElement {
  return Boolean(
    element &&
      (element.tagName === 'TEXTAREA' ||
        element.tagName === 'INPUT' ||
        element.tagName === 'SELECT' ||
        (element instanceof HTMLElement && element.isContentEditable)),
  )
}

function isEditableTarget(target: EventTarget | null): boolean {
  return target instanceof Element && isEditableElement(target)
}

/** Owns the review workspace keymap and lets defineShortcuts clean it up with the view scope. */
export function useReviewShortcuts(options: UseReviewShortcutsOptions) {
  const {
    canPrev,
    canNext,
    busy,
    notesEditor,
    sourceEmailPanel,
    shortcutsHelpOpen,
    requirementCount,
  } = options

  function isNotesFocused() {
    return notesEditor.value?.isFocused() ?? false
  }

  async function toggleNotes() {
    if (shortcutsHelpOpen.value) {
      return
    }

    if (isNotesFocused()) {
      notesEditor.value?.blur()
      await options.saveNotes()
      return
    }

    if (!isEditableElement(document.activeElement)) {
      notesEditor.value?.focus()
    }
  }

  function exitEditing() {
    if (!isEditableElement(document.activeElement)) {
      return
    }

    document.activeElement.blur()
    void options.saveNotes()
  }

  function isArmed(event?: KeyboardEvent) {
    return (
      !shortcutsHelpOpen.value &&
      !isEditableTarget(event?.target ?? null) &&
      !isEditableElement(document.activeElement)
    )
  }

  const shortcuts = computed<Record<string, ShortcutDefinition>>(() => {
    const definitions: Record<string, ShortcutDefinition> = {
      ArrowLeft: (event) => {
        if (isArmed(event) && canPrev.value && !busy.value) {
          void options.onPrev()
        }
      },
      ArrowRight: (event) => {
        if (isArmed(event) && canNext.value && !busy.value) {
          void options.onNext()
        }
      },
      E: (event) => {
        if (isArmed(event)) {
          options.onEditDetails()
        }
      },
      O: (event) => {
        if (isArmed(event)) {
          sourceEmailPanel.value?.toggle()
        }
      },
      S: (event) => {
        if (isArmed(event) && !busy.value) {
          void options.onDecide('shortlisted')
        }
      },
      F: (event) => {
        if (isArmed(event) && !busy.value) {
          void options.onDecide('flagged')
        }
      },
      R: (event) => {
        if (isArmed(event) && !busy.value) {
          void options.onDecide('rejected')
        }
      },
      N: {
        handler: () => {
          void toggleNotes()
        },
      },
      Escape: {
        usingInput: true,
        handler: () => {
          if (shortcutsHelpOpen.value) {
            shortcutsHelpOpen.value = false
            return
          }
          if (sourceEmailPanel.value?.isOpen()) {
            sourceEmailPanel.value.close()
            return
          }
          exitEditing()
        },
      },
      '?': (event) => {
        if (isArmed(event)) {
          shortcutsHelpOpen.value = true
        }
      },
    }

    requirementShortcutKeys.slice(0, requirementCount.value).forEach((shortcut, index) => {
      definitions[shortcut] = (event) => {
        if (isArmed(event) && !busy.value) {
          void options.onToggleRequirement(index)
        }
      }
    })

    return definitions
  })

  defineShortcuts(shortcuts)
}