import { createRouter, createWebHistory } from 'vue-router'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      name: 'vacancy-list',
      component: () => import('@/pages/vacancies/VacancyListView.vue'),
    },
    {
      path: '/vacancies/:id',
      name: 'vacancy-detail',
      component: () => import('@/pages/vacancy-detail/VacancyDetailView.vue'),
      props: true,
    },
    {
      path: '/vacancies/:id/review/:candidateId',
      name: 'candidate-review',
      component: () => import('@/pages/review/ReviewView.vue'),
      props: true,
    },
  ],
})
