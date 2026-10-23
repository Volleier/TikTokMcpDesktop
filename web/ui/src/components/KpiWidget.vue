<template>
  <div class="kpi">
    <div class="value">{{ display }}</div>
    <div class="unit muted" v-if="payload?.unit">{{ payload.unit }}</div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
const props = defineProps<{ payload?: { value?: unknown; unit?: string } | null }>()
const display = computed(() => {
  const v = props.payload?.value
  if (v === null || v === undefined || v === '') return '—'
  return String(v)
})
</script>

<style scoped>
.kpi { display: flex; align-items: baseline; gap: 8px; }
.value { font-size: 28px; font-weight: 700; letter-spacing: -0.02em; }
.unit { font-size: 13px; }
</style>
