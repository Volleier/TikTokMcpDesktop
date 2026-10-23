<template>
  <div class="layout">
    <aside class="card side">
      <div class="side-head">
        <strong>数据表</strong>
        <button class="secondary" @click="loadTables" :disabled="loading">刷新</button>
      </div>
      <div v-if="!tables.length" class="empty">暂无表。请先同步 MCP，或用 DevHost 演示样本。</div>
      <button
        v-for="t in tables"
        :key="t.id"
        class="table-item"
        :class="{ active: t.id === tableId }"
        @click="selectTable(t.id)"
      >
        <div class="name">{{ t.name }}</div>
        <div class="muted tiny">{{ t.sourceTool }}</div>
        <div class="muted tiny">{{ formatTime(t.lastSyncedAt) }}</div>
      </button>
    </aside>

    <section class="card main" v-if="tableId && detail">
      <div class="toolbar">
        <div>
          <h2>{{ detail.table.name }}</h2>
          <div class="muted tiny">MCP 工具 {{ detail.table.sourceTool }} · {{ page?.total ?? 0 }} 行</div>
        </div>
        <div class="actions">
          <select v-model="filterField">
            <option value="">筛选列</option>
            <option v-for="f in visibleFields" :key="f.id" :value="f.id">{{ f.name }}</option>
          </select>
          <select v-model="filterOp">
            <option value="contains">包含</option>
            <option value="equals">等于</option>
            <option value="notEquals">不等于</option>
            <option value="greaterThan">大于</option>
            <option value="lessThan">小于</option>
            <option value="isEmpty">为空</option>
            <option value="isNotEmpty">非空</option>
          </select>
          <input v-model="filterValue" placeholder="筛选值" @keyup.enter="reloadRecords" />
          <select v-model="sortField">
            <option value="">排序列</option>
            <option v-for="f in visibleFields" :key="f.id" :value="f.id">{{ f.name }}</option>
          </select>
          <select v-model="sortDir">
            <option value="asc">升序</option>
            <option value="desc">降序</option>
          </select>
          <button @click="reloadRecords">查询</button>
          <button class="secondary" @click="doExport">导出 CSV</button>
        </div>
      </div>

      <div class="col-toggles">
        <label v-for="f in detail.fields" :key="f.id" class="tiny">
          <input type="checkbox" v-model="columnVisible[f.id]" /> {{ f.name }}
        </label>
      </div>

      <div class="grid-wrap">
        <table>
          <thead>
            <tr>
              <th v-for="f in shownFields" :key="f.id" :style="{ minWidth: f.width + 'px' }">{{ f.name }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="row in page?.items || []" :key="row.id">
              <td v-for="f in shownFields" :key="f.id">{{ formatCell(row.values[f.id]) }}</td>
            </tr>
          </tbody>
        </table>
        <div v-if="!(page?.items?.length)" class="empty">无匹配行</div>
      </div>

      <div class="pager">
        <button class="secondary" :disabled="!canPrev" @click="prevPage">上一页</button>
        <span class="muted tiny">offset {{ cursor || '0' }}</span>
        <button class="secondary" :disabled="!page?.hasMore" @click="nextPage">下一页</button>
      </div>
    </section>
    <section v-else class="card main empty">选择左侧数据表开始浏览</section>
  </div>
  <p v-if="error" class="err">{{ error }}</p>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api, type DataTableDetail, type DataTableMeta, type RecordPage } from '../api/client'

function errMessage(e: unknown): string {
  if (e instanceof Error) return e.message
  return String(e)
}

const route = useRoute()
const router = useRouter()
const tables = ref<DataTableMeta[]>([])
const detail = ref<DataTableDetail | null>(null)
const page = ref<RecordPage | null>(null)
const loading = ref(false)
const error = ref('')
const tableId = computed(() => (route.params.id as string) || '')
const filterField = ref('')
const filterOp = ref('contains')
const filterValue = ref('')
const sortField = ref('')
const sortDir = ref('asc')
const cursor = ref<string | undefined>(undefined)
const columnVisible = reactive<Record<string, boolean>>({})

const visibleFields = computed(() => detail.value?.fields.filter(f => f.visible) || [])
const shownFields = computed(() =>
  (detail.value?.fields || []).filter(f => columnVisible[f.id] !== false)
)
const canPrev = computed(() => !!cursor.value && Number(cursor.value) > 0)

function formatTime(v?: string | null) {
  if (!v) return '从未同步'
  try { return new Date(v).toLocaleString() } catch { return v }
}
function formatCell(v: unknown) {
  if (v === null || v === undefined) return ''
  if (typeof v === 'number') {
    return Number.isInteger(v)
      ? v.toLocaleString('zh-CN')
      : v.toLocaleString('zh-CN', { maximumFractionDigits: 2 })
  }
  if (typeof v === 'object') return JSON.stringify(v)
  return String(v)
}

async function loadTables() {
  loading.value = true
  error.value = ''
  try {
    tables.value = await api.listTables()
    if (!tableId.value && tables.value[0]) {
      await router.replace(`/tables/${tables.value[0].id}`)
    }
  } catch (e: unknown) {
    error.value = errMessage(e)
  } finally {
    loading.value = false
  }
}

async function selectTable(id: string) {
  await router.push(`/tables/${id}`)
}

async function loadDetailAndRecords() {
  if (!tableId.value) return
  error.value = ''
  try {
    detail.value = await api.getTable(tableId.value)
    detail.value.fields.forEach(f => { columnVisible[f.id] = f.visible })
    cursor.value = undefined
    await reloadRecords()
  } catch (e: unknown) {
    error.value = errMessage(e)
  }
}

async function reloadRecords() {
  if (!tableId.value) return
  page.value = await api.records(tableId.value, {
    cursor: cursor.value,
    pageSize: 50,
    filterField: filterField.value,
    filterOp: filterOp.value,
    filterValue: filterValue.value,
    sortField: sortField.value,
    sortDir: sortDir.value
  })
}

function nextPage() {
  if (!page.value?.hasMore) return
  cursor.value = page.value.nextCursor || undefined
  reloadRecords()
}
function prevPage() {
  const n = Math.max(0, Number(cursor.value || 0) - 50)
  cursor.value = n === 0 ? undefined : String(n)
  reloadRecords()
}

async function doExport() {
  if (!tableId.value) return
  try { await api.exportCsv(tableId.value) }
  catch (e: unknown) { error.value = errMessage(e) }
}

const onDataChanged = () => { loadTables().then(loadDetailAndRecords) }

watch(tableId, loadDetailAndRecords)
onMounted(() => {
  loadTables().then(loadDetailAndRecords)
  window.addEventListener('ttmcp-data-changed', onDataChanged)
})
onUnmounted(() => window.removeEventListener('ttmcp-data-changed', onDataChanged))
</script>

<style scoped>
.layout { display: grid; grid-template-columns: 260px 1fr; gap: 16px; height: calc(100vh - 96px); }
.side { overflow: auto; }
.side-head { display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px; }
.table-item {
  display: block; width: 100%; text-align: left; background: transparent; color: var(--text);
  border: 1px solid transparent; margin-bottom: 6px; box-shadow: none; border-radius: 12px;
}
.table-item .name { font-weight: 600; }
.table-item.active, .table-item:hover {
  background: var(--accent-soft);
  border-color: rgba(0, 242, 234, 0.35);
  transform: none;
}
.main { display: flex; flex-direction: column; min-width: 0; overflow: hidden; }
.toolbar { display: flex; justify-content: space-between; gap: 12px; flex-wrap: wrap; margin-bottom: 10px; }
.toolbar h2 {
  margin: 0;
  font-size: 20px;
  font-family: "Sora", sans-serif;
  letter-spacing: -0.03em;
}
.actions { display: flex; flex-wrap: wrap; gap: 6px; align-items: center; }
.col-toggles { display: flex; flex-wrap: wrap; gap: 10px; margin-bottom: 8px; color: var(--muted); }
.grid-wrap {
  flex: 1; overflow: auto;
  border: 1px solid var(--border);
  border-radius: 12px;
  background: rgba(7, 11, 20, 0.45);
}
table { border-collapse: collapse; width: max-content; min-width: 100%; }
th, td {
  border-bottom: 1px solid var(--border);
  padding: 9px 12px;
  text-align: left;
  font-size: 13px;
  white-space: nowrap;
  max-width: 360px;
  overflow: hidden;
  text-overflow: ellipsis;
}
th {
  position: sticky; top: 0; z-index: 1;
  background: rgba(18, 24, 38, 0.96);
  color: #cbd5e1;
}
tbody tr:hover td { background: rgba(0, 242, 234, 0.05); }
.pager { display: flex; gap: 10px; align-items: center; margin-top: 10px; }
.tiny { font-size: 12px; }
@media (max-width: 900px) {
  .layout { grid-template-columns: 1fr; height: auto; }
}
</style>
