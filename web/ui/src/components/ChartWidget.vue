<template>
  <div ref="el" class="chart" :class="{ tall: type === 'chart-tree' }"></div>
</template>

<script setup lang="ts">
import * as echarts from 'echarts'
import { onMounted, onUnmounted, ref, watch } from 'vue'

type SeriesPayload = { labels?: string[]; values?: number[] }
type TreeNode = { name: string; value?: number; children?: TreeNode[] }

const props = defineProps<{
  type: string
  payload?: SeriesPayload | TreeNode | null
}>()

const el = ref<HTMLDivElement | null>(null)
let chart: echarts.ECharts | null = null

const palette = ['#FF2D55', '#00F2EA', '#FFB020', '#7CFFB2', '#8B5CF6', '#38BDF8', '#F472B6', '#A3E635']

function render() {
  if (!el.value) return
  if (!chart) chart = echarts.init(el.value, undefined, { renderer: 'canvas' })

  if (props.type === 'chart-tree') {
    const tree = props.payload as TreeNode | null | undefined
    const root = tree?.name
      ? tree
      : { name: '暂无结构', children: [] as TreeNode[] }
    chart.setOption({
      tooltip: { trigger: 'item', triggerOn: 'mousemove' },
      series: [{
        type: 'tree',
        data: [root],
        top: '4%',
        left: '8%',
        bottom: '4%',
        right: '22%',
        symbol: 'emptyCircle',
        symbolSize: 9,
        orient: 'LR',
        expandAndCollapse: true,
        initialTreeDepth: 2,
        lineStyle: { color: 'rgba(255,255,255,0.28)', width: 1.2, curveness: 0.45 },
        label: {
          position: 'left',
          verticalAlign: 'middle',
          align: 'right',
          fontSize: 12,
          color: '#E8EEF7',
          fontFamily: 'IBM Plex Sans, sans-serif'
        },
        leaves: {
          label: { position: 'right', align: 'left' }
        },
        emphasis: { focus: 'descendant' },
        animationDuration: 450,
        animationDurationUpdate: 350
      }]
    }, true)
    return
  }

  const series = props.payload as SeriesPayload | null | undefined
  const labels = series?.labels || []
  const values = (series?.values || []).map(Number)
  const isPie = props.type === 'chart-pie'

  if (isPie) {
    chart.setOption({
      color: palette,
      tooltip: { trigger: 'item', formatter: '{b}<br/>{c} ({d}%)' },
      legend: {
        type: 'scroll',
        bottom: 0,
        textStyle: { color: '#94A3B8', fontSize: 11 }
      },
      series: [{
        type: 'pie',
        radius: ['42%', '68%'],
        center: ['50%', '46%'],
        avoidLabelOverlap: true,
        itemStyle: { borderRadius: 6, borderColor: 'rgba(10,14,22,0.85)', borderWidth: 2 },
        label: { color: '#CBD5E1', fontSize: 11 },
        data: labels.map((name, i) => ({ name, value: values[i] ?? 0 }))
      }]
    }, true)
    return
  }

  const isBar = props.type === 'chart-bar'
  chart.setOption({
    color: palette,
    tooltip: { trigger: 'axis' },
    grid: { left: 48, right: 18, top: 28, bottom: 36 },
    xAxis: {
      type: 'category',
      data: labels,
      axisLabel: { color: '#94A3B8', hideOverlap: true, fontSize: 11 },
      axisLine: { lineStyle: { color: 'rgba(148,163,184,0.35)' } }
    },
    yAxis: {
      type: 'value',
      splitLine: { lineStyle: { color: 'rgba(148,163,184,0.12)' } },
      axisLabel: { color: '#94A3B8', fontSize: 11 }
    },
    series: [{
      type: isBar ? 'bar' : 'line',
      data: values,
      smooth: true,
      barWidth: isBar ? '48%' : undefined,
      itemStyle: {
        color: isBar
          ? new echarts.graphic.LinearGradient(0, 0, 0, 1, [
              { offset: 0, color: '#00F2EA' },
              { offset: 1, color: '#FF2D55' }
            ])
          : '#00F2EA',
        borderRadius: isBar ? [6, 6, 0, 0] : 0
      },
      areaStyle: !isBar
        ? {
            color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
              { offset: 0, color: 'rgba(0,242,234,0.35)' },
              { offset: 1, color: 'rgba(0,242,234,0.02)' }
            ])
          }
        : undefined,
      lineStyle: { width: 2.5 }
    }]
  }, true)
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
.chart { height: 260px; width: 100%; }
.chart.tall { height: 420px; }
</style>
