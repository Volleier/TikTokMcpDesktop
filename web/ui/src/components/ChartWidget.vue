<template>
  <div ref="el" class="chart"></div>
</template>

<script setup lang="ts">
import * as echarts from 'echarts'
import { onMounted, onUnmounted, ref, watch } from 'vue'

const props = defineProps<{ type: string; payload?: { labels?: string[]; values?: number[] } | null }>()
const el = ref<HTMLDivElement | null>(null)
let chart: echarts.ECharts | null = null

function render() {
  if (!el.value) return
  if (!chart) chart = echarts.init(el.value)
  const labels = props.payload?.labels || []
  const values = props.payload?.values || []
  const isPie = props.type === 'chart-pie'
  if (isPie) {
    chart.setOption({
      tooltip: { trigger: 'item' },
      series: [{
        type: 'pie',
        radius: ['35%', '65%'],
        data: labels.map((name, i) => ({ name, value: values[i] ?? 0 }))
      }]
    }, true)
  } else {
    chart.setOption({
      tooltip: { trigger: 'axis' },
      grid: { left: 40, right: 16, top: 24, bottom: 32 },
      xAxis: { type: 'category', data: labels },
      yAxis: { type: 'value' },
      series: [{
        type: props.type === 'chart-bar' ? 'bar' : 'line',
        data: values,
        smooth: true,
        itemStyle: { color: '#0f766e' },
        areaStyle: props.type === 'chart-line' ? { color: 'rgba(15,118,110,0.12)' } : undefined
      }]
    }, true)
  }
}

const onResize = () => chart?.resize()
onMounted(() => {
  render()
  window.addEventListener('resize', onResize)
})
onUnmounted(() => {
  window.removeEventListener('resize', onResize)
  chart?.dispose()
})
watch(() => [props.type, props.payload], render, { deep: true })
</script>

<style scoped>
.chart { height: 240px; width: 100%; }
</style>
