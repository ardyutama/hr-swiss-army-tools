<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import AppButton from '@/shared/ui/AppButton.vue'
import StatCard from '@/shared/ui/StatCard.vue'
import VacancyTable from './components/VacancyTable.vue'
import { listVacancies, type VacancySummary } from './api'

const vacancies = ref<VacancySummary[] | null>(null)
const error = ref<string | null>(null)

onMounted(async () => {
  try {
    vacancies.value = await listVacancies()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to load vacancies'
  }
})

// Layout prototype: status/candidates are placeholders until the API exposes them.
const rows = computed(() =>
  (vacancies.value ?? []).map((v) => ({
    id: v.id,
    role: v.title,
    status: 'Open' as const,
    candidates: '0/30',
    date: new Date(v.createdOn).toLocaleDateString(),
  })),
)
</script>

<template>
  <div class="vacancy-page">
    <div class="vacancy-page__title-row">
      <h1 class="vacancy-page__title">Vacancies</h1>
      <p class="vacancy-page__subtitle">Sorting CV — each vacancy collects and sorts its candidates</p>
    </div>

    <header class="vacancy-page__header">
      <StatCard label="Open vacancies" :value="vacancies?.length ?? 0" />
      <StatCard label="CVs to sort" value="0" hint="placeholder" />
      <AppButton class="vacancy-page__add">+ Add vacancy</AppButton>
    </header>

    <section class="vacancy-page__panel">
      <p v-if="error" role="alert">{{ error }}</p>
      <p v-else-if="vacancies === null">Loading…</p>
      <p v-else-if="vacancies.length === 0">No vacancies yet.</p>
      <VacancyTable v-else :rows="rows" />
    </section>
  </div>
</template>

<style scoped>
.vacancy-page {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.vacancy-page__title {
  margin: 0;
  font-size: 1.5rem;
}

.vacancy-page__subtitle {
  margin: 0.25rem 0 0;
  color: var(--muted);
  font-size: 0.9rem;
}

.vacancy-page__header {
  display: flex;
  gap: 1rem;
  align-items: stretch;
}

.vacancy-page__add {
  margin-left: auto;
  align-self: flex-start;
}

.vacancy-page__panel {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  box-shadow: var(--shadow);
  padding: 0.5rem 0.5rem 0.25rem;
  min-height: 24rem;
}

.vacancy-page__panel p {
  padding: 1rem;
  color: var(--muted);
}
</style>
