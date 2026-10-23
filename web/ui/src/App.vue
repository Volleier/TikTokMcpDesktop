<template>
  <div class="shell">
    <header class="top">
      <div class="brand">
        <span class="mark" aria-hidden="true"></span>
        <div>
          <div class="title">TikTok MCP</div>
          <div class="sub">Open MCP · 本机表格与看板</div>
        </div>
      </div>
      <nav>
        <RouterLink to="/tables">表格</RouterLink>
        <RouterLink to="/dashboards">看板</RouterLink>
      </nav>
      <div class="status">{{ status }}</div>
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
    status.value = r.ok ? 'LocalApi 在线' : 'LocalApi 异常'
  } catch {
    status.value = 'LocalApi 离线'
  }
  try {
    es = new EventSource('/api/events')
    es.addEventListener('data-changed', () => {
      status.value = '数据已更新 · ' + new Date().toLocaleTimeString()
      window.dispatchEvent(new CustomEvent('ttmcp-data-changed'))
    })
    es.onerror = () => { status.value = '事件流重连中…' }
  } catch { /* ignore */ }
})

onUnmounted(() => es?.close())
</script>

<style scoped>
.shell { height: 100%; display: flex; flex-direction: column; position: relative; z-index: 1; }
.top {
  display: flex; align-items: center; gap: 18px;
  padding: 12px 18px;
  background: rgba(10, 14, 22, 0.72);
  border-bottom: 1px solid var(--border);
  backdrop-filter: blur(16px);
}
.brand { display: flex; align-items: center; gap: 12px; }
.mark {
  width: 34px; height: 34px; border-radius: 11px;
  background:
    radial-gradient(circle at 30% 30%, #fff 0 12%, transparent 13%),
    linear-gradient(135deg, #ff2d55, #00f2ea);
  box-shadow: 0 0 0 1px rgba(255,255,255,0.12), 0 10px 24px rgba(255, 45, 85, 0.35);
}
.title {
  font-family: "Sora", sans-serif;
  font-weight: 700;
  letter-spacing: -0.03em;
  font-size: 16px;
}
.sub { font-size: 11px; color: var(--muted); margin-top: 1px; }
nav { display: flex; gap: 8px; }
nav a {
  color: var(--muted);
  text-decoration: none;
  padding: 8px 12px;
  border-radius: 999px;
  border: 1px solid transparent;
  transition: 0.15s ease;
}
nav a:hover { color: var(--text); border-color: var(--border); }
nav a.router-link-active {
  background: var(--accent-soft);
  color: var(--accent);
  border-color: rgba(0, 242, 234, 0.35);
  font-weight: 600;
}
.status { margin-left: auto; font-size: 12px; color: var(--muted); }
main { flex: 1; min-height: 0; padding: 18px; overflow: auto; }
</style>
