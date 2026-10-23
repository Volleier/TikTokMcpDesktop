import { createRouter, createWebHashHistory } from 'vue-router'
import TablesView from './views/TablesView.vue'
import DashboardsView from './views/DashboardsView.vue'

export const router = createRouter({
  history: createWebHashHistory(),
  routes: [
    { path: '/', redirect: '/tables' },
    { path: '/tables', component: TablesView },
    { path: '/tables/:id', component: TablesView },
    { path: '/dashboards', component: DashboardsView },
    { path: '/dashboards/:id', component: DashboardsView }
  ]
})
