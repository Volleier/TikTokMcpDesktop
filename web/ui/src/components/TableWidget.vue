<template>
  <div class="wrap">
    <div v-if="!rows.length" class="muted">无数据</div>
    <table v-else>
      <thead>
        <tr>
          <th v-for="c in columns" :key="c">{{ labelOf(c) }}</th>
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

const props = defineProps<{ payload?: unknown }>()

const labels: Record<string, string> = {
  campaign: '广告系列',
  objective: '目标',
  objective_type: '目标',
  spend: '消耗',
  impressions: '展示',
  clicks: '点击',
  conversion: '转化',
  ctr: 'CTR%',
  cpc: 'CPC',
  cpa: 'CPA',
  name: '名称',
  sourceTool: '来源工具',
  lastSyncedAt: '最近同步',
  advertiser_id: '广告主 ID',
  campaign_id: '系列 ID',
  campaign_name: '系列名称',
  adgroup_name: '广告组',
  ad_name: '广告',
  operation_status: '状态',
  budget: '预算',
  currency: '币种',
  industry: '行业',
  balance: '余额',
  stat_time_day: '日期'
}

const rows = computed<Record<string, unknown>[]>(() => {
  const p = props.payload
  if (!Array.isArray(p)) return []
  return p.map((x) => (x && typeof x === 'object' ? x as Record<string, unknown> : { value: x }))
})
const columns = computed(() => {
  const keys = new Set<string>()
  rows.value.forEach(r => Object.keys(r).forEach(k => keys.add(k)))
  return Array.from(keys).slice(0, 10)
})
function labelOf(c: string) { return labels[c] || c }
function format(v: unknown) {
  if (v === null || v === undefined) return ''
  if (typeof v === 'number') {
    return Number.isInteger(v)
      ? v.toLocaleString('zh-CN')
      : v.toLocaleString('zh-CN', { maximumFractionDigits: 2 })
  }
  if (typeof v === 'object') return JSON.stringify(v)
  return String(v)
}
</script>

<style scoped>
.wrap { overflow: auto; max-height: 320px; border-radius: 10px; border: 1px solid var(--border); }
table { border-collapse: collapse; width: 100%; font-size: 12.5px; }
th, td {
  border-bottom: 1px solid var(--border);
  padding: 9px 12px;
  text-align: left;
  white-space: nowrap;
}
th {
  position: sticky; top: 0; z-index: 1;
  background: rgba(18, 24, 38, 0.96);
  color: #CBD5E1;
  font-weight: 600;
  letter-spacing: 0.02em;
}
tbody tr:hover td { background: rgba(0, 242, 234, 0.06); }
td { color: #E2E8F0; }
</style>
