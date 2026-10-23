<template>
  <div class="layout">
    <aside class="card side">
      <div class="side-title">数据看板</div>
      <p class="muted tiny intro">基于 TikTok Open MCP 只读工具入库结果</p>
      <button
        v-for="d in dashboards"
        :key="d.id"
        class="item"
        :class="{ active: d.id === currentId }"
        @click="select(d.id)"
      >
        <div class="name">{{ d.name }}</div>
        <div class="muted tiny">{{ d.description }}</div>
      </button>
    </aside>

    <section class="main" v-if="layout && data">
      <div class="head card">
        <div>
          <h2>{{ layout.name }}</h2>
          <p class="muted" v-if="data.empty">{{ data.message }}</p>
          <p class="muted tiny" v-else>图表与表格随 LocalApi 数据实时刷新</p>
        </div>
        <button class="secondary" @click="reload">刷新</button>
      </div>

      <div class="widgets">
        <div
          v-for="w in layout.widgets"
          :key="w.id"
          class="card widget"
          :class="[`span-${Math.min(w.colSpan || 1, 2)}`, w.type === 'kpi' ? 'kpi-card' : '']"
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
.layout { display: grid; grid-template-columns: 240px 1fr; gap: 16px; }
.side { display: flex; flex-direction: column; gap: 8px; height: fit-content; position: sticky; top: 0; }
.side-title { font-family: "Sora", sans-serif; font-weight: 700; letter-spacing: -0.02em; }
.intro { margin: 0 0 8px; }
.item {
  text-align: left; background: transparent; color: var(--text);
  border: 1px solid transparent; box-shadow: none; border-radius: 12px;
}
.item.active, .item:hover {
  background: var(--accent-soft);
  border-color: rgba(0, 242, 234, 0.35);
  transform: none;
}
.name { font-weight: 600; margin-bottom: 2px; }
.head {
  display: flex; justify-content: space-between; align-items: center;
  margin-bottom: 14px;
}
.head h2 {
  margin: 0;
  font-family: "Sora", sans-serif;
  letter-spacing: -0.03em;
  font-size: 22px;
}
.widgets {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}
.widget { min-height: 120px; animation: rise 0.45s ease both; }
.widget:nth-child(1) { animation-delay: 0.02s; }
.widget:nth-child(2) { animation-delay: 0.05s; }
.widget:nth-child(3) { animation-delay: 0.08s; }
.widget:nth-child(4) { animation-delay: 0.11s; }
.span-1 { grid-column: span 1; }
.span-2 { grid-column: span 2; }
.kpi-card {
  background:
    linear-gradient(145deg, rgba(0, 242, 234, 0.08), transparent 42%),
    var(--panel);
}
.w-title {
  font-weight: 600;
  margin-bottom: 10px;
  color: #c7d2e4;
  font-size: 13px;
  letter-spacing: 0.02em;
}
.tiny { font-size: 12px; }
@keyframes rise {
  from { opacity: 0; transform: translateY(8px); }
  to { opacity: 1; transform: none; }
}
@media (max-width: 900px) {
  .layout { grid-template-columns: 1fr; }
  .widgets { grid-template-columns: 1fr; }
  .span-1, .span-2 { grid-column: span 1; }
  .side { position: static; }
}
</style>
