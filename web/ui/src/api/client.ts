export interface DataTableMeta {
  id: string
  name: string
  sourceTool: string
  lastSyncedAt?: string | null
}

export interface DataField {
  id: string
  tableId: string
  name: string
  type: string
  width: number
  visible: boolean
  sort: number
}

export interface DataTableDetail {
  table: DataTableMeta
  fields: DataField[]
  activeView: { id: string; visibleFieldIds: string[] }
}

export interface DataRecord {
  id: string
  tableId: string
  values: Record<string, unknown>
  updatedAt: string
}

export interface RecordPage {
  items: DataRecord[]
  total: number
  nextCursor?: string | null
  hasMore: boolean
}

export interface DashboardSummary {
  id: string
  name: string
  description: string
}

export interface DashboardLayout {
  id: string
  name: string
  widgets: Array<{ id: string; type: string; title: string; colSpan: number; metric?: string }>
}

export interface DashboardData {
  id: string
  widgets: Record<string, any>
  empty: boolean
  message?: string | null
}

async function json<T>(url: string, init?: RequestInit): Promise<T> {
  const r = await fetch(url, init)
  if (!r.ok) {
    const t = await r.text()
    throw new Error(t || r.statusText)
  }
  return r.json() as Promise<T>
}

export const api = {
  listTables: () => json<DataTableMeta[]>('/api/tables'),
  getTable: (id: string) => json<DataTableDetail>(`/api/tables/${id}`),
  records: (id: string, params: Record<string, string | number | undefined>) => {
    const q = new URLSearchParams()
    Object.entries(params).forEach(([k, v]) => {
      if (v !== undefined && v !== '') q.set(k, String(v))
    })
    return json<RecordPage>(`/api/tables/${id}/records?${q}`)
  },
  exportCsv: async (id: string) => {
    const r = await fetch(`/api/tables/${id}/export/csv`, { method: 'POST' })
    if (!r.ok) throw new Error(await r.text())
    const blob = await r.blob()
    const cd = r.headers.get('content-disposition') || ''
    const match = /filename="?([^"]+)"?/i.exec(cd)
    const name = match?.[1] || `export-${id}.csv`
    const a = document.createElement('a')
    a.href = URL.createObjectURL(blob)
    a.download = name
    a.click()
    URL.revokeObjectURL(a.href)
  },
  listDashboards: () => json<DashboardSummary[]>('/api/dashboards'),
  getDashboard: (id: string) => json<DashboardLayout>(`/api/dashboards/${id}`),
  dashboardData: (id: string) => json<DashboardData>(`/api/dashboards/${id}/data`)
}
