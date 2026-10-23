<template>
  <div class="kpi">
    <div class="value">{{ display }}</div>
    <div class="unit" v-if="payload?.unit">{{ payload.unit }}</div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
const props = defineProps<{ payload?: { value?: unknown; unit?: string } | null }>()
const display = computed(() => {
  const v = props.payload?.value
  if (v === null || v === undefined || v === '') return '—'
  if (typeof v === 'number') {
    return Number.isInteger(v)
      ? v.toLocaleString('zh-CN')
      : v.toLocaleString('zh-CN', { maximumFractionDigits: 2 })
  }
  return String(v)
})
</script>

<style scoped>
.kpi { display: flex; align-items: baseline; gap: 8px; min-height: 44px; }
.value {
  font-family: "Sora", "IBM Plex Sans", sans-serif;
  font-size: 30px;
  font-weight: 650;
  letter-spacing: -0.03em;
  background: linear-gradient(120deg, #fff 20%, #00F2EA 70%, #FF2D55);
  -webkit-background-clip: text;
  background-clip: text;
  color: transparent;
}
.unit { font-size: 12px; color: var(--muted); text-transform: uppercase; letter-spacing: 0.06em; }
</style>
