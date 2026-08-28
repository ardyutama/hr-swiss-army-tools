<script setup lang="ts">
import { useRoute } from 'vue-router'

export interface NavItem {
  label: string
  to?: string
  soon?: boolean
}

export interface NavSection {
  label: string
  items: NavItem[]
}

defineProps<{
  sections: NavSection[]
}>()

const route = useRoute()
</script>

<template>
  <nav class="sidebar" aria-label="Main navigation">
    <div class="sidebar__logo">HR·SAT</div>

    <section v-for="section in sections" :key="section.label" class="sidebar__section">
      <h2 class="sidebar__section-label">{{ section.label }}</h2>
      <ul class="sidebar__nav">
        <li v-for="item in section.items" :key="item.label">
          <RouterLink
            v-if="item.to"
            class="sidebar__link"
            :class="{ 'sidebar__link--active': route.path === item.to }"
            :to="item.to"
          >
            {{ item.label }}
          </RouterLink>
          <span v-else class="sidebar__link sidebar__link--soon">
            {{ item.label }}
            <span v-if="item.soon" class="sidebar__soon">soon</span>
          </span>
        </li>
      </ul>
    </section>
  </nav>
</template>

<style scoped>
.sidebar {
  display: flex;
  flex-direction: column;
  gap: 1.75rem;
}

.sidebar__logo {
  border: 2px solid var(--sidebar-active);
  border-radius: var(--radius);
  padding: 0.75rem;
  text-align: center;
  font-weight: 700;
  letter-spacing: 0.1em;
  color: var(--sidebar-active);
}

.sidebar__section-label {
  margin: 0 0 0.5rem;
  font-size: 0.72rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.1em;
  color: var(--muted);
}

.sidebar__nav {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.sidebar__link {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.6rem 0.75rem;
  border-radius: 8px;
  color: var(--sidebar-text);
  text-decoration: none;
  font-size: 0.925rem;
}

.sidebar__link:hover {
  background: rgb(255 255 255 / 0.07);
}

.sidebar__link--active {
  background: var(--accent);
  color: var(--sidebar-active);
  font-weight: 600;
}

.sidebar__link--soon {
  color: var(--muted);
}

.sidebar__soon {
  font-size: 0.68rem;
  border: 1px solid rgb(255 255 255 / 0.2);
  border-radius: 999px;
  padding: 0.1rem 0.45rem;
}
</style>
