<template>
  <div class="layout">
    <aside class="card side">
      <strong>数据看板</strong>
      <button
        v-for="d in dashboards"
        :key="d.id"
        class="item"
        :class="{ active: d.id === currentId }"
        @click="select(d.id)"
      >
        <div>{{ d.name }}</div>
        <div class="muted tiny">{{ d.description }}</div>
      </button>
    </aside>

    <section class="main" v-if="layout && data">
      <div class="head card">
        <div>
          <h2>{{ layout.name }}</h2>
          <p class="muted" v-if="data.empty">{{ data.message }}</p>
        </div>
        <button class="secondary" @click="reload">刷新</button>
      </div>

      <div class="widgets">
        <div
          v-for="w in layout.widgets"
          :key="w.id"
          class="card widget"
          :style="{ gridColumn: `span ${Math.min(w.colSpan || 1, 2)}` }"
        >
          <div class="w-title">{{ w.title }}</div>
          <KpiWidget v-if="w.type === 'kpi'" :payload="data.widgets[w.id]" />
          <ChartWidget v-else-if="w.type.startsWith('chart-')" :type="w.type" :payload="data.widgets[w.id]" />
          <TableWidget v-else-if="w.type === 'data-table'" :payload="data.widgets[w.id]" />
          <div v-else class="muted">未知组件 {{ w.type }}</div>
        </div>
      </div>
    </section>
    <section v-else class="card empty">加载看板…</section>
  </div>
  <p v-if="error" class="err">{{ error }}</p>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api, type DashboardData, type DashboardLayout, type DashboardSummary } from '../api/client'
import KpiWidget from '../components/KpiWidget.vue'
import ChartWidget from '../components/ChartWidget.vue'
import TableWidget from '../components/TableWidget.vue'

const route = useRoute()
const router = useRouter()
const dashboards = ref<DashboardSummary[]>([])
const layout = ref<DashboardLayout | null>(null)
const data = ref<DashboardData | null>(null)
const error = ref('')
const currentId = computed(() => (route.params.id as string) || '')

async function bootstrap() {
  dashboards.value = await api.listDashboards()
  if (!currentId.value && dashboards.value[0]) {
    await router.replace(`/dashboards/${dashboards.value[0].id}`)
  }
}

async function select(id: string) {
  await router.push(`/dashboards/${id}`)
}

async function reload() {
  if (!currentId.value) return
  error.value = ''
  try {
    layout.value = await api.getDashboard(currentId.value)
    data.value = await api.dashboardData(currentId.value)
  } catch (e: any) {
    error.value = e.message || String(e)
  }
}

const onDataChanged = () => reload()
watch(currentId, reload)
onMounted(async () => {
  try {
    await bootstrap()
    await reload()
  } catch (e: any) {
    error.value = e.message || String(e)
  }
  window.addEventListener('ttmcp-data-changed', onDataChanged)
})
onUnmounted(() => window.removeEventListener('ttmcp-data-changed', onDataChanged))
</script>

<style scoped>
.layout { display: grid; grid-template-columns: 220px 1fr; gap: 16px; }
.side { display: flex; flex-direction: column; gap: 8px; height: fit-content; }
.item {
  text-align: left; background: transparent; color: var(--text); border: 1px solid transparent;
}
.item.active, .item:hover { background: var(--accent-soft); border-color: #99f6e4; }
.head { display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px; }
.head h2 { margin: 0; }
.widgets { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; }
.widget { min-height: 120px; }
.w-title { font-weight: 600; margin-bottom: 10px; }
.tiny { font-size: 12px; }
@media (max-width: 900px) {
  .layout { grid-template-columns: 1fr; }
  .widgets { grid-template-columns: 1fr; }
}
</style>
