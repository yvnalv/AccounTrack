import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/login',
      name: 'login',
      component: () => import('@/views/LoginView.vue'),
      meta: { public: true },
    },
    {
      path: '/',
      component: () => import('@/layouts/AppShell.vue'),
      children: [
        { path: '', name: 'dashboard', component: () => import('@/views/DashboardView.vue') },
        { path: 'sales', name: 'sales', component: () => import('@/views/sales/SalesOrdersView.vue'), meta: { titleKey: 'sales.title' } },
        { path: 'sales/new', name: 'salesOrderCreate', component: () => import('@/views/sales/SalesOrderCreateView.vue'), meta: { titleKey: 'sales.new' } },
        { path: 'sales/:id', name: 'salesOrderDetail', component: () => import('@/views/sales/SalesOrderDetailView.vue'), meta: { titleKey: 'sales.title' } },
        { path: 'purchasing', name: 'purchasing', component: () => import('@/views/PlaceholderView.vue'), meta: { titleKey: 'nav.purchasing' } },
        { path: 'inventory', name: 'inventory', component: () => import('@/views/PlaceholderView.vue'), meta: { titleKey: 'nav.inventory' } },
        { path: 'accounting', name: 'accounting', component: () => import('@/views/PlaceholderView.vue'), meta: { titleKey: 'nav.accounting' } },
        { path: 'master-data', name: 'masterData', component: () => import('@/views/PlaceholderView.vue'), meta: { titleKey: 'nav.masterData' } },
        { path: 'approvals', name: 'approvals', component: () => import('@/views/PlaceholderView.vue'), meta: { titleKey: 'nav.approvals' } },
        { path: 'settings', name: 'settings', component: () => import('@/views/PlaceholderView.vue'), meta: { titleKey: 'nav.settings' } },
      ],
    },
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],
})

router.beforeEach((to) => {
  const auth = useAuthStore()
  if (!to.meta.public && !auth.isAuthenticated) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }
  if (to.name === 'login' && auth.isAuthenticated) {
    return { name: 'dashboard' }
  }
  return true
})

export default router
