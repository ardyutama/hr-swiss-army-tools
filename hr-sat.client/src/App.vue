<script setup lang="ts">
import { Toaster } from 'vue-sonner'
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
  <Toaster
    position="top-right"
    rich-colors
    close-button
    container-aria-label="Notifications"
  />
  <AppShell>
    <template #sidebar>
      <AppSidebar :sections="navSections" />
    </template>
    <router-view />
  </AppShell>
</template>
