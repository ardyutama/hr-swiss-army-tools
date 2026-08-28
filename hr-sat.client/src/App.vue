<script setup lang="ts">
import AppShell from '@/shared/ui/AppShell.vue'
import AppSidebar, { type NavSection } from '@/shared/ui/AppSidebar.vue'

const navSections: NavSection[] = [
  {
    label: 'Sorting CV',
    items: [
      { label: 'Vacancies', to: '/' },
      { label: 'Templates', soon: true },
    ],
  },
]
</script>

<template>
  <AppShell>
    <template #sidebar>
      <AppSidebar :sections="navSections" />
    </template>
    <router-view />
  </AppShell>
</template>
