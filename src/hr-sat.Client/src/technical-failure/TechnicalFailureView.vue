<script setup lang="ts">
import { useRouter } from 'vue-router'

const router = useRouter()

function backToVacancies() {
  void router.push({ name: 'vacancy-list' })
}

function reloadApp() {
  window.location.reload()
}
</script>

<template>
  <section
    aria-labelledby="technical-failure-heading"
    class="flex min-h-full items-center justify-center px-4 py-12 sm:px-8"
  >
    <div class="w-full max-w-xl text-center">
      <UIcon name="i-lucide-circle-alert" class="mx-auto size-10 text-error" aria-hidden="true" />
      <h1 id="technical-failure-heading" class="mt-5 text-2xl font-bold text-highlighted">
        Something went wrong
      </h1>
      <p class="mx-auto mt-3 max-w-lg text-base leading-7 text-muted">
        This section hit an unexpected problem and couldn't recover on its own. Your work in other
        sections is safe.
      </p>
      <div class="mt-8 flex flex-wrap justify-center gap-3">
        <UButton color="error" icon="i-lucide-arrow-left" @click="backToVacancies">
          Back to Vacancies
        </UButton>
        <UButton color="neutral" variant="outline" icon="i-lucide-refresh-cw" @click="reloadApp">
          Reload app
        </UButton>
      </div>
    </div>
  </section>
</template>