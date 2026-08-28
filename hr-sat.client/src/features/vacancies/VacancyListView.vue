<script setup lang="ts">
import { onMounted, ref } from 'vue'
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
</script>

<template>
  <main>
    <h1>Vacancies</h1>
    <p v-if="error" role="alert">{{ error }}</p>
    <p v-else-if="vacancies === null">Loading…</p>
    <p v-else-if="vacancies.length === 0">No vacancies yet.</p>
    <ul v-else>
      <li v-for="v in vacancies" :key="v.id">{{ v.title }}</li>
    </ul>
  </main>
</template>
