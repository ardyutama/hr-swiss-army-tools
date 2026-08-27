import { createRouter, createWebHistory } from 'vue-router'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      name: 'vacancy-list',
      component: () => import('@/features/vacancies/VacancyListView.vue'),
    },
  ],
})
