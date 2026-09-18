import { createRouter, createWebHistory } from 'vue-router'
import PrototypeView from '@/modules/trading/views/PrototypeView/PrototypeView.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      name: 'prototype',
      component: PrototypeView,
    },
  ],
})

export default router