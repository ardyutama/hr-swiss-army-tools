<script setup lang="ts">
interface NavItem {
  label: string
  to: string
}

interface NavSection {
  label: string
  items: NavItem[]
}

// ADR-0008 decision 15: no dead UI — upcoming nav items live as tickets, not placeholders.
const navSections: NavSection[] = [
  {
    label: 'Sorting CV',
    items: [{ label: 'Vacancies', to: '/' }],
  },
]
</script>

<template>
  <UApp>
    <div class="grid min-h-screen grid-cols-1 bg-default text-highlighted md:grid-cols-[15rem_minmax(0,1fr)]">
      <aside class="border-b border-default bg-sidebar px-4 py-6 text-sidebar-foreground md:min-h-screen md:border-b-0 md:border-r">
        <nav class="flex flex-col gap-7" aria-label="Main navigation">
          <div class="rounded-xl border-2 border-sidebar-active px-3 py-3 text-center font-bold tracking-[0.1em] text-sidebar-active">
            HR·SAT
          </div>

          <section v-for="section in navSections" :key="section.label">
            <h2 class="mb-2 text-xs font-bold uppercase tracking-[0.1em] text-sidebar-foreground/70">
              {{ section.label }}
            </h2>
            <ul class="m-0 flex list-none flex-col gap-1 p-0">
              <li v-for="item in section.items" :key="item.label">
                <RouterLink
                  :to="item.to"
                  exact-active-class="bg-primary font-semibold text-sidebar-active"
                  class="flex items-center justify-between rounded-lg px-3 py-2.5 text-sm text-sidebar-foreground no-underline transition-colors hover:bg-white/10"
                >
                  {{ item.label }}
                </RouterLink>
              </li>
            </ul>
          </section>
        </nav>
      </aside>

      <main class="min-w-0 bg-default px-4 py-6 sm:px-8 sm:py-7 lg:px-9">
        <router-view />
      </main>
    </div>
  </UApp>
</template>
