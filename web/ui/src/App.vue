<template>
  <div class="shell">
    <header class="top">
      <div class="brand">TikTok MCP Data</div>
      <nav>
        <RouterLink to="/tables">表格</RouterLink>
        <RouterLink to="/dashboards">看板</RouterLink>
      </nav>
      <div class="status muted">{{ status }}</div>
    </header>
    <main>
      <RouterView />
    </main>
  </div>
</template>

<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'
import { RouterLink, RouterView } from 'vue-router'

const status = ref('connecting…')
let es: EventSource | null = null

onMounted(async () => {
  try {
    const r = await fetch('/api/health')
    status.value = r.ok ? 'LocalApi ok' : 'LocalApi error'
  } catch {
    status.value = 'LocalApi offline'
  }
  try {
    es = new EventSource('/api/events')
    es.addEventListener('data-changed', () => {
      status.value = 'data updated ' + new Date().toLocaleTimeString()
      window.dispatchEvent(new CustomEvent('ttmcp-data-changed'))
    })
    es.onerror = () => { status.value = 'events reconnecting…' }
  } catch { /* ignore */ }
})

onUnmounted(() => es?.close())
</script>

<style scoped>
.shell { height: 100%; display: flex; flex-direction: column; }
.top {
  display: flex; align-items: center; gap: 16px;
  padding: 10px 16px; background: var(--panel); border-bottom: 1px solid var(--border);
}
.brand { font-weight: 650; }
nav { display: flex; gap: 12px; }
nav a { color: var(--muted); text-decoration: none; padding: 6px 10px; border-radius: 6px; }
nav a.router-link-active { background: var(--accent-soft); color: var(--accent); font-weight: 600; }
.status { margin-left: auto; font-size: 12px; }
main { flex: 1; min-height: 0; padding: 16px; overflow: auto; }
</style>
