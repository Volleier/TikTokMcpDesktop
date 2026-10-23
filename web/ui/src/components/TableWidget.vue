<template>
  <div class="wrap">
    <div v-if="!rows.length" class="muted">无数据</div>
    <table v-else>
      <thead>
        <tr>
          <th v-for="c in columns" :key="c">{{ c }}</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="(row, i) in rows" :key="i">
          <td v-for="c in columns" :key="c">{{ format(row[c]) }}</td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{ payload?: any }>()
const rows = computed<Record<string, any>[]>(() => {
  const p = props.payload
  if (!Array.isArray(p)) return []
  return p.map((x) => (x && typeof x === 'object' ? x : { value: x }))
})
const columns = computed(() => {
  const keys = new Set<string>()
  rows.value.forEach(r => Object.keys(r).forEach(k => keys.add(k)))
  return Array.from(keys).slice(0, 8)
})
function format(v: unknown) {
  if (v === null || v === undefined) return ''
  if (typeof v === 'object') return JSON.stringify(v)
  return String(v)
}
</script>

<style scoped>
.wrap { overflow: auto; max-height: 280px; }
table { border-collapse: collapse; width: 100%; font-size: 12px; }
th, td { border-bottom: 1px solid var(--border); padding: 6px 8px; text-align: left; white-space: nowrap; }
th { background: #f8fafc; position: sticky; top: 0; }
</style>
