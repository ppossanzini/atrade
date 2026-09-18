import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import TradingShell from '@/layouts/TradingShell/TradingShell.vue'
import LoginView from '@/modules/auth/views/LoginView/LoginView.vue'
import OperationalStatusView from '@/modules/operations/views/OperationalStatusView/OperationalStatusView.vue'
import BasketBuilderView from '@/modules/basket/views/BasketBuilderView/BasketBuilderView.vue'
import ComingSoonView from '@/modules/common/views/ComingSoonView/ComingSoonView.vue'
import { useSessionStore } from '@/stores/session'

/**
 * Sections that are not implemented yet still have a route, so no sidebar entry is ever dead.
 */
const pendingSections: RouteRecordRaw[] = [
  {
    path: 'strategy',
    name: 'strategy',
    component: ComingSoonView,
    meta: { titleKey: 'navigation.strategy' },
  },
  {
    path: 'market',
    name: 'market',
    component: ComingSoonView,
    meta: { titleKey: 'navigation.market' },
  },
  {
    path: 'execution',
    name: 'execution',
    component: ComingSoonView,
    meta: { titleKey: 'navigation.execution' },
  },
  {
    path: 'history',
    name: 'history',
    component: ComingSoonView,
    meta: { titleKey: 'navigation.history' },
  },
  {
    path: 'journal',
    name: 'journal',
    component: ComingSoonView,
    meta: { titleKey: 'navigation.journal' },
  },
]

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'login',
    component: LoginView,
    meta: { requiresAuth: false },
  },
  {
    path: '/',
    component: TradingShell,
    meta: { requiresAuth: true },
    children: [
      { path: '', redirect: { name: 'status' } },
      {
        path: 'status',
        name: 'status',
        component: OperationalStatusView,
        meta: { titleKey: 'navigation.status' },
      },
      {
        path: 'basket',
        name: 'basket',
        component: BasketBuilderView,
        meta: { titleKey: 'navigation.basket' },
      },
      ...pendingSections,
    ],
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: { name: 'status' },
  },
]

export const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
})

router.beforeEach((to) => {
  const sessionStore = useSessionStore()
  const requiresAuthentication = to.matched.some((record) => record.meta.requiresAuth === true)

  if (requiresAuthentication && !sessionStore.isAuthenticated) {
    return { name: 'login' }
  }

  if (!requiresAuthentication && sessionStore.isAuthenticated) {
    return { name: 'status' }
  }

  return true
})
