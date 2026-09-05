<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, shallowRef, useTemplateRef, watch } from 'vue'
import VuePdfEmbed from 'vue-pdf-embed'
import type { CvDocumentResult } from '@/features/candidates/api'

const props = defineProps<{
  documents: CvDocumentResult[]
}>()

const selectedId = shallowRef<number | null>(null)
const selectedDocument = computed(
  () =>
    props.documents.find((document) => document.id === selectedId.value) ??
    props.documents.find((document) => document.isPrimary) ??
    props.documents[0] ??
    null,
)

const page = shallowRef(1)
const pageCount = shallowRef(0)
const loading = shallowRef(true)
const failed = shallowRef(false)
const reloadKey = shallowRef(0)

// 100% = fit the viewer width; zoom steps of 25% within 50%–300%.
const zoom = shallowRef(1)
const zoomPercent = computed(() => `${Math.round(zoom.value * 100)}%`)
const canZoomOut = computed(() => zoom.value > 0.5)
const canZoomIn = computed(() => zoom.value < 3)

const viewer = useTemplateRef<HTMLDivElement>('viewer')
const containerWidth = shallowRef(0)
const pageWidth = computed(() =>
  containerWidth.value > 0 ? Math.floor(containerWidth.value * zoom.value) : undefined,
)

let resizeObserver: ResizeObserver | null = null

onMounted(() => {
  if (typeof ResizeObserver === 'undefined' || !viewer.value) {
    return
  }
  resizeObserver = new ResizeObserver((entries) => {
    const width = entries[0]?.contentRect.width ?? 0
    if (width > 0) {
      containerWidth.value = width
    }
  })
  resizeObserver.observe(viewer.value)
})

onBeforeUnmount(() => resizeObserver?.disconnect())

function onLoaded(pdf: { numPages: number }) {
  pageCount.value = pdf.numPages
  loading.value = false
  failed.value = false
}

function onLoadingFailed() {
  loading.value = false
  failed.value = true
}

function retry() {
  loading.value = true
  failed.value = false
  reloadKey.value += 1
}

function goToPage(target: number) {
  if (target >= 1 && target <= pageCount.value) {
    page.value = target
  }
}

function zoomBy(delta: number) {
  zoom.value = Math.min(3, Math.max(0.5, Math.round((zoom.value + delta) * 100) / 100))
}

function selectDocument(id: number) {
  if (id !== selectedDocument.value?.id) {
    selectedId.value = id
  }
}

// A different document (candidate switch or manual selection) restarts at page 1.
watch(
  () => selectedDocument.value?.id,
  () => {
    page.value = 1
    pageCount.value = 0
    loading.value = true
    failed.value = false
  },
)
</script>

<template>
  <section class="flex min-w-0 flex-col" aria-label="CV viewer">
    <div class="rounded-xl border border-default bg-default shadow-sm">
      <!-- Toolbar at the top: page/zoom stay reachable while scrolling a tall PDF. -->
      <div
        v-if="selectedDocument"
        class="flex flex-wrap items-center gap-x-3 gap-y-2 border-b border-default p-2"
      >
        <!-- Document switcher only appears when the candidate has several CVs. -->
        <template v-if="documents.length > 1">
          <select
            :value="selectedDocument?.id"
            aria-label="Choose CV document"
            class="min-h-10 min-w-0 max-w-64 flex-1 truncate rounded-xl border border-default bg-default px-3 py-2 text-sm text-highlighted"
            @change="selectDocument(Number(($event.target as HTMLSelectElement).value))"
          >
            <option v-for="document in documents" :key="document.id" :value="document.id">
              {{ document.originalFilename }}
            </option>
          </select>
          <UBadge v-if="selectedDocument?.isPrimary" color="primary" variant="subtle">
            Primary
          </UBadge>
        </template>

        <div class="ml-auto flex items-center gap-1">
          <UButton
            icon="i-lucide-chevron-left"
            color="neutral"
            variant="outline"
            :disabled="page <= 1"
            aria-label="Previous page"
            @click="goToPage(page - 1)"
          />
          <span class="min-w-14 text-center text-sm tabular-nums text-muted">
            {{ page }} / {{ pageCount || '–' }}
          </span>
          <UButton
            icon="i-lucide-chevron-right"
            color="neutral"
            variant="outline"
            :disabled="pageCount === 0 || page >= pageCount"
            aria-label="Next page"
            @click="goToPage(page + 1)"
          />
        </div>
        <div class="flex items-center gap-1">
          <UButton
            icon="i-lucide-minus"
            color="neutral"
            variant="outline"
            :disabled="!canZoomOut"
            aria-label="Zoom out"
            @click="zoomBy(-0.25)"
          />
          <span class="min-w-12 text-center text-sm tabular-nums text-muted">
            {{ zoomPercent }}
          </span>
          <UButton
            icon="i-lucide-plus"
            color="neutral"
            variant="outline"
            :disabled="!canZoomIn"
            aria-label="Zoom in"
            @click="zoomBy(0.25)"
          />
        </div>
      </div>

      <div ref="viewer" class="flex min-h-[32rem] items-start justify-center overflow-auto p-3">
        <template v-if="selectedDocument">
          <div
            v-if="failed"
            class="flex flex-col items-center gap-3 self-center py-16 text-center"
            role="alert"
          >
            <UIcon name="i-lucide-file-x-2" class="size-8 text-muted" aria-hidden="true" />
            <p class="m-0 text-sm text-muted">Couldn't load the CV document.</p>
            <UButton color="neutral" variant="outline" @click="retry">Try again</UButton>
          </div>
          <USkeleton
            v-show="!failed && loading"
            class="aspect-[210/297] w-full max-w-2xl"
            aria-busy="true"
            aria-label="Loading CV"
          />
          <VuePdfEmbed
            v-show="!failed && !loading"
            :key="`${selectedDocument.id}:${reloadKey}`"
            :source="selectedDocument.downloadUrl"
            :page="page"
            :width="pageWidth"
            @loaded="onLoaded"
            @loading-failed="onLoadingFailed"
          />
        </template>
        <p v-else class="self-center text-sm text-muted">This candidate has no CV document.</p>
      </div>
    </div>
  </section>
</template>
