<script setup lang="ts" generic="T">
defineProps<{
  columns: { key: string; label: string }[]
  rows: T[]
}>()
</script>

<template>
  <table class="table">
    <thead>
      <tr>
        <th v-for="col in columns" :key="col.key" scope="col">{{ col.label }}</th>
      </tr>
    </thead>
    <tbody>
      <tr v-for="(row, index) in rows" :key="index">
        <td v-for="col in columns" :key="col.key">
          <slot :name="`cell-${col.key}`" :row="row">
            {{ String(row[col.key as keyof T] ?? '') }}
          </slot>
        </td>
      </tr>
    </tbody>
  </table>
</template>

<style scoped>
.table {
  width: 100%;
  border-collapse: collapse;
}

th {
  text-align: left;
  font-size: 0.8rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--muted);
  padding: 0.75rem 1rem;
  border-bottom: 2px solid var(--border);
}

td {
  padding: 0.85rem 1rem;
  border-bottom: 1px solid var(--border);
  font-size: 0.925rem;
}

tbody tr:hover {
  background: var(--bg);
}
</style>
