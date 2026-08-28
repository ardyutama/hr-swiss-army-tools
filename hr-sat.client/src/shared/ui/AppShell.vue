<template>
  <div class="shell">
    <aside class="shell__sidebar">
      <slot name="sidebar" />
    </aside>
    <main class="shell__main">
      <slot />
    </main>
  </div>
</template>

<style scoped>
.shell {
  display: grid;
  grid-template-columns: 240px 1fr;
  min-height: 100vh;
}

.shell__sidebar {
  background: var(--sidebar-bg);
  color: var(--sidebar-text);
  padding: 1.5rem 1.1rem;
}

.shell__main {
  padding: 1.75rem 2.25rem;
  min-width: 0;
}
</style>
