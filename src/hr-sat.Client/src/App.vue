<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { useLocalStorage } from '@vueuse/core'
import type { NavigationMenuItem } from '@nuxt/ui'

const route = useRoute()
const sidebarOpen = useLocalStorage('sidebar-open', true)

// ADR-0008 decision 15: no dead UI — upcoming nav items live as tickets, not placeholders.
// Routes are flat, so vue-router never activates '/' on /vacancies/*; the Vacancies item
// owns its active rule explicitly and stays lit through the whole vacancy flow.
const navItems = computed<NavigationMenuItem[]>(() => [
  { label: 'Sorting CV', type: 'label' },
  {
    label: 'Vacancies',
    icon: 'i-lucide-briefcase',
    to: '/',
    active: route.path === '/' || route.path.startsWith('/vacancies'),
  },
])
</script>

<template>
  <UApp>
    <UDashboardGroup unit="px" :persistent="false">
      <UDashboardSidebar
        v-model:open="sidebarOpen"
        collapsible
        :default-size="240"
        class="app-sidebar bg-sidebar"
        :menu="{ close: false }"
        :ui="{
          header: 'px-3',
          body: 'px-3',
          content: 'app-sidebar bg-sidebar',
        }"
      >
        <template #header="{ collapsed }">
          <RouterLink
            v-if="!collapsed"
            to="/"
            class="rounded-md text-base font-bold tracking-tight text-sidebar-active focus-visible:outline-2 focus-visible:outline-sidebar-active/50"
          >
            HR <span class="font-semibold text-sidebar-foreground/70">SAT</span>
          </RouterLink>
          <UDashboardSidebarCollapse square class="ms-auto" />
        </template>

        <template #default="{ collapsed }">
          <UNavigationMenu
            :collapsed="collapsed"
            :items="navItems"
            orientation="vertical"
            color="neutral"
            :ui="{
              label: 'px-3 font-bold uppercase tracking-[0.1em] text-sidebar-foreground/60',
              link: 'px-3 py-2.5 before:rounded-xl',
            }"
          />
        </template>
      </UDashboardSidebar>

      <UDashboardPanel :ui="{ body: 'gap-0 p-4 sm:px-8 sm:py-7 lg:px-9' }">
        <template #header>
          <UDashboardNavbar title="HR SAT" class="lg:hidden" />
        </template>

        <template #body>
          <router-view />
        </template>
      </UDashboardPanel>
    </UDashboardGroup>
  </UApp>
</template>
