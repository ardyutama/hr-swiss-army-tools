import { readonly, shallowRef, type App } from 'vue'
import type { Router } from 'vue-router'
import { useToast } from '@nuxt/ui/composables/useToast'

const technicalFailureState = shallowRef(false)

export const technicalFailure = readonly(technicalFailureState)

const toastMessage = {
  title: 'Something went wrong',
  description: 'This section hit a technical failure. Your work elsewhere is intact.',
  color: 'error' as const,
}

let unhandledRejectionListenerInstalled = false

function showTechnicalFailureToast() {
  useToast().add({ ...toastMessage })
}

function handleFatalFailure(error: unknown) {
  console.error('Technical failure', error)
  technicalFailureState.value = true
  showTechnicalFailureToast()
}

function handleUnhandledRejection(event: PromiseRejectionEvent) {
  console.error('Unhandled promise rejection', event.reason)
  showTechnicalFailureToast()
}

export function installTechnicalFailureHandling(app: App, router: Router) {
  app.config.errorHandler = (error) => {
    handleFatalFailure(error)
  }

  router.onError((error) => {
    handleFatalFailure(error)
  })

  router.afterEach((_to, _from, failure) => {
    if (!failure) {
      technicalFailureState.value = false
    }
  })

  if (typeof window !== 'undefined' && !unhandledRejectionListenerInstalled) {
    window.addEventListener('unhandledrejection', handleUnhandledRejection)
    unhandledRejectionListenerInstalled = true
  }
}