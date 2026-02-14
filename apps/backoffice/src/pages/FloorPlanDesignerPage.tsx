import { useState, useEffect, useRef, useCallback } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useBookings } from '../contexts/BookingContext'
import type { Table, TableShape } from '../api/tables'
import type { FloorPlan, FloorPlanElement, FloorPlanElementType } from '../api/floorPlans'
import * as floorPlanApi from '../api/floorPlans'
import * as tableApi from '../api/tables'

// ============================================================================
// Types
// ============================================================================

type SelectedItem =
  | { kind: 'table'; id: string }
  | { kind: 'element'; id: string }
  | null

interface DragState {
  active: boolean
  kind: 'table' | 'element'
  id: string
  offsetX: number
  offsetY: number
}

// ============================================================================
// Constants
// ============================================================================

const GRID_SIZE = 20
const TABLE_DEFAULTS: Record<TableShape, { w: number; h: number; seats: number }> = {
  Square: { w: 80, h: 80, seats: 4 },
  Rectangle: { w: 120, h: 80, seats: 6 },
  Round: { w: 90, h: 90, seats: 4 },
  Oval: { w: 120, h: 80, seats: 6 },
  Bar: { w: 180, h: 50, seats: 5 },
  Booth: { w: 120, h: 80, seats: 4 },
  Custom: { w: 80, h: 80, seats: 4 },
}

const STATUS_COLORS: Record<string, string> = {
  Available: '#4caf50',
  Reserved: '#ff9800',
  Occupied: '#e57373',
  Dirty: '#bdbdbd',
  Blocked: '#9e9e9e',
  OutOfService: '#757575',
}

function snapToGrid(v: number): number {
  return Math.round(v / GRID_SIZE) * GRID_SIZE
}

// ============================================================================
// Seat positions calculator
// ============================================================================

function getSeatPositions(shape: TableShape, w: number, h: number, count: number): { cx: number; cy: number }[] {
  const seatR = 10
  const gap = seatR + 4
  const seats: { cx: number; cy: number }[] = []

  if (shape === 'Round' || shape === 'Oval') {
    const rx = w / 2
    const ry = h / 2
    for (let i = 0; i < count; i++) {
      const angle = (2 * Math.PI * i) / count - Math.PI / 2
      seats.push({
        cx: rx + (rx + gap) * Math.cos(angle),
        cy: ry + (ry + gap) * Math.sin(angle),
      })
    }
    return seats
  }

  if (shape === 'Bar') {
    const spacing = w / (count + 1)
    for (let i = 0; i < count; i++) {
      seats.push({ cx: spacing * (i + 1), cy: -gap })
    }
    return seats
  }

  // Rectangular / Square: distribute seats around perimeter
  const perimeter = 2 * (w + h)
  for (let i = 0; i < count; i++) {
    const d = (perimeter * i) / count
    if (d < w) {
      seats.push({ cx: d, cy: -gap }) // top
    } else if (d < w + h) {
      seats.push({ cx: w + gap, cy: d - w }) // right
    } else if (d < 2 * w + h) {
      seats.push({ cx: 2 * w + h - d, cy: h + gap }) // bottom
    } else {
      seats.push({ cx: -gap, cy: perimeter - d }) // left
    }
  }
  return seats
}

// ============================================================================
// SVG Table Component
// ============================================================================

function TableSvg({ table, isSelected, onMouseDown }: {
  table: Table
  isSelected: boolean
  onMouseDown: (e: React.MouseEvent) => void
}) {
  const pos = table.position ?? { x: 50, y: 50 }
  const defaults = TABLE_DEFAULTS[table.shape] ?? TABLE_DEFAULTS.Square
  const w = table.position?.width ?? defaults.w
  const h = table.position?.height ?? defaults.h
  const fill = STATUS_COLORS[table.status] ?? '#4caf50'
  const seats = getSeatPositions(table.shape, w, h, table.maxCapacity)
  const rotation = table.position?.rotation ?? 0

  return (
    <g
      className="table-group"
      transform={`translate(${pos.x}, ${pos.y})${rotation ? ` rotate(${rotation}, ${w / 2}, ${h / 2})` : ''}`}
      onMouseDown={onMouseDown}
    >
      {/* Selection ring */}
      {isSelected && (
        table.shape === 'Round' ? (
          <circle
            cx={w / 2} cy={h / 2} r={w / 2 + 6}
            fill="none" stroke="#2196f3" strokeWidth="2.5" strokeDasharray="6 3"
          />
        ) : table.shape === 'Oval' ? (
          <ellipse
            cx={w / 2} cy={h / 2} rx={w / 2 + 6} ry={h / 2 + 6}
            fill="none" stroke="#2196f3" strokeWidth="2.5" strokeDasharray="6 3"
          />
        ) : (
          <rect
            x={-4} y={-4} width={w + 8} height={h + 8} rx={8}
            fill="none" stroke="#2196f3" strokeWidth="2.5" strokeDasharray="6 3"
          />
        )
      )}

      {/* Shadow */}
      {table.shape === 'Round' ? (
        <circle cx={w / 2 + 2} cy={h / 2 + 2} r={w / 2} fill="rgba(0,0,0,0.08)" />
      ) : table.shape === 'Oval' ? (
        <ellipse cx={w / 2 + 2} cy={h / 2 + 2} rx={w / 2} ry={h / 2} fill="rgba(0,0,0,0.08)" />
      ) : (
        <rect x={2} y={2} width={w} height={h} rx={6} fill="rgba(0,0,0,0.08)" />
      )}

      {/* Table shape */}
      {table.shape === 'Round' ? (
        <circle
          cx={w / 2} cy={h / 2} r={w / 2}
          fill={fill} stroke="white" strokeWidth="2" opacity={0.85}
        />
      ) : table.shape === 'Oval' ? (
        <ellipse
          cx={w / 2} cy={h / 2} rx={w / 2} ry={h / 2}
          fill={fill} stroke="white" strokeWidth="2" opacity={0.85}
        />
      ) : (
        <rect
          x={0} y={0} width={w} height={h}
          rx={table.shape === 'Bar' ? 25 : 6}
          fill={fill} stroke="white" strokeWidth="2" opacity={0.85}
        />
      )}

      {/* Table number */}
      <text
        x={w / 2} y={h / 2 - 2}
        textAnchor="middle" dominantBaseline="middle"
        fill="white" fontSize="14" fontWeight="700"
        style={{ pointerEvents: 'none', userSelect: 'none' }}
      >
        {table.number}
      </text>

      {/* Capacity */}
      <text
        x={w / 2} y={h / 2 + 13}
        textAnchor="middle" dominantBaseline="middle"
        fill="rgba(255,255,255,0.8)" fontSize="10"
        style={{ pointerEvents: 'none', userSelect: 'none' }}
      >
        {table.maxCapacity} seats
      </text>

      {/* Seat circles */}
      {seats.map((s, i) => (
        <circle
          key={i} cx={s.cx} cy={s.cy} r={7}
          fill="white" stroke={fill} strokeWidth="1.5" opacity={0.9}
        />
      ))}

      {/* Combined indicator */}
      {table.isCombinable && (
        <circle
          cx={w - 4} cy={4} r={5}
          fill="#2196f3" stroke="white" strokeWidth="1"
        />
      )}
    </g>
  )
}

// ============================================================================
// SVG Structural Element Component
// ============================================================================

function ElementSvg({ element, isSelected, onMouseDown }: {
  element: FloorPlanElement
  isSelected: boolean
  onMouseDown: (e: React.MouseEvent) => void
}) {
  const rotation = element.rotation ?? 0

  return (
    <g
      className="element-group"
      transform={`translate(${element.x}, ${element.y})${rotation ? ` rotate(${rotation}, ${element.width / 2}, ${element.height / 2})` : ''}`}
      onMouseDown={onMouseDown}
    >
      {isSelected && (
        <rect
          x={-3} y={-3}
          width={element.width + 6} height={element.height + 6}
          rx={3}
          fill="none" stroke="#2196f3" strokeWidth="2" strokeDasharray="5 3"
        />
      )}

      {element.type === 'Wall' && (
        <rect
          x={0} y={0}
          width={element.width} height={element.height}
          rx={2}
          fill="#455a64" stroke="#37474f" strokeWidth="1"
        />
      )}

      {element.type === 'Door' && (
        <>
          <rect
            x={0} y={0}
            width={element.width} height={element.height}
            fill="#81d4fa" stroke="#0288d1" strokeWidth="1.5" rx={3}
            opacity={0.5}
          />
          {/* Door swing arc */}
          <path
            d={`M 0,${element.height} A ${element.width},${element.width} 0 0,1 ${element.width},0`}
            fill="none" stroke="#0288d1" strokeWidth="1" strokeDasharray="3 2"
            opacity={0.6}
          />
        </>
      )}

      {element.type === 'Divider' && (
        <line
          x1={0} y1={element.height / 2}
          x2={element.width} y2={element.height / 2}
          stroke="#90a4ae" strokeWidth="3" strokeDasharray="8 4"
          strokeLinecap="round"
        />
      )}

      {element.label && (
        <text
          x={element.width / 2} y={element.height + 14}
          textAnchor="middle" fontSize="10"
          fill="#78909c"
          style={{ pointerEvents: 'none', userSelect: 'none' }}
        >
          {element.label}
        </text>
      )}
    </g>
  )
}

// ============================================================================
// Merge Connectors
// ============================================================================

function MergeConnectors({ tables }: { tables: Table[] }) {
  const rendered = new Set<string>()
  const lines: { x1: number; y1: number; x2: number; y2: number; key: string }[] = []

  for (const t of tables) {
    if (!t.position) continue
    const tw = t.position.width ?? TABLE_DEFAULTS[t.shape]?.w ?? 80
    const th = t.position.height ?? TABLE_DEFAULTS[t.shape]?.h ?? 80

    for (const otherId of (t as Table & { combinedWith?: string[] }).combinedWith ?? []) {
      const pairKey = [t.id, otherId].sort().join('-')
      if (rendered.has(pairKey)) continue
      rendered.add(pairKey)

      const other = tables.find(o => o.id === otherId)
      if (!other?.position) continue
      const ow = other.position.width ?? TABLE_DEFAULTS[other.shape]?.w ?? 80
      const oh = other.position.height ?? TABLE_DEFAULTS[other.shape]?.h ?? 80

      lines.push({
        x1: t.position.x + tw / 2,
        y1: t.position.y + th / 2,
        x2: other.position.x + ow / 2,
        y2: other.position.y + oh / 2,
        key: pairKey,
      })
    }
  }

  return (
    <>
      {lines.map(l => (
        <line
          key={l.key}
          x1={l.x1} y1={l.y1} x2={l.x2} y2={l.y2}
          className="merge-connector"
          stroke="#2196f3" strokeWidth="2.5"
        />
      ))}
    </>
  )
}

// ============================================================================
// Palette Panel
// ============================================================================

function Palette({ onAddTable, onAddElement }: {
  onAddTable: (shape: TableShape) => void
  onAddElement: (type: FloorPlanElementType) => void
}) {
  return (
    <div className="floor-plan-palette">
      <div className="palette-section">
        <h4>Tables</h4>
        <button className="palette-item" onClick={() => onAddTable('Square')}>
          <svg width="20" height="20"><rect x="2" y="2" width="16" height="16" rx="2" fill="#4caf50" opacity="0.7" /></svg>
          Square
        </button>
        <button className="palette-item" onClick={() => onAddTable('Rectangle')}>
          <svg width="20" height="20"><rect x="1" y="4" width="18" height="12" rx="2" fill="#4caf50" opacity="0.7" /></svg>
          Rectangle
        </button>
        <button className="palette-item" onClick={() => onAddTable('Round')}>
          <svg width="20" height="20"><circle cx="10" cy="10" r="8" fill="#4caf50" opacity="0.7" /></svg>
          Round
        </button>
        <button className="palette-item" onClick={() => onAddTable('Oval')}>
          <svg width="20" height="20"><ellipse cx="10" cy="10" rx="9" ry="6" fill="#4caf50" opacity="0.7" /></svg>
          Oval
        </button>
        <button className="palette-item" onClick={() => onAddTable('Bar')}>
          <svg width="20" height="20"><rect x="1" y="7" width="18" height="6" rx="3" fill="#4caf50" opacity="0.7" /></svg>
          Bar
        </button>
      </div>

      <div className="palette-section">
        <h4>Structure</h4>
        <button className="palette-item" onClick={() => onAddElement('Wall')}>
          <svg width="20" height="20"><rect x="2" y="8" width="16" height="4" rx="1" fill="#455a64" /></svg>
          Wall
        </button>
        <button className="palette-item" onClick={() => onAddElement('Door')}>
          <svg width="20" height="20"><rect x="2" y="4" width="16" height="12" rx="2" fill="#81d4fa" opacity="0.6" stroke="#0288d1" strokeWidth="1" /></svg>
          Door
        </button>
        <button className="palette-item" onClick={() => onAddElement('Divider')}>
          <svg width="20" height="20"><line x1="2" y1="10" x2="18" y2="10" stroke="#90a4ae" strokeWidth="2" strokeDasharray="4 2" /></svg>
          Divider
        </button>
      </div>
    </div>
  )
}

// ============================================================================
// Properties Panel
// ============================================================================

function PropertiesPanel({ selected, tables, elements, floorPlan, onUpdateTable, onDeleteTable, onUpdateElement, onDeleteElement }: {
  selected: SelectedItem
  tables: Table[]
  elements: FloorPlanElement[]
  floorPlan: FloorPlan
  onUpdateTable: (tableId: string, updates: Parameters<typeof tableApi.updateTable>[1]) => void
  onDeleteTable: (tableId: string) => void
  onUpdateElement: (elementId: string, updates: Parameters<typeof floorPlanApi.updateElement>[2]) => void
  onDeleteElement: (elementId: string) => void
}) {
  if (!selected) {
    return (
      <div className="floor-plan-properties">
        <h4>{floorPlan.name}</h4>
        <p style={{ color: 'var(--pico-muted-color)', fontSize: '0.85rem' }}>
          {floorPlan.width} x {floorPlan.height}
        </p>
        <hr />
        <p style={{ color: 'var(--pico-muted-color)', fontSize: '0.85rem' }}>
          {tables.length} table{tables.length !== 1 ? 's' : ''} &middot; {elements.length} element{elements.length !== 1 ? 's' : ''}
        </p>
        <p style={{ color: 'var(--pico-muted-color)', fontSize: '0.8rem', marginTop: '1rem' }}>
          Click a table or element to view its properties. Drag items to reposition them on the canvas.
        </p>
      </div>
    )
  }

  if (selected.kind === 'table') {
    const table = tables.find(t => t.id === selected.id)
    if (!table) return null

    return (
      <div className="floor-plan-properties">
        <h4>Table {table.number}</h4>
        <label>
          Number
          <input
            type="text"
            value={table.number}
            onChange={e => onUpdateTable(table.id, { number: e.target.value })}
          />
        </label>
        <label>
          Name
          <input
            type="text"
            value={table.name ?? ''}
            placeholder="Optional name"
            onChange={e => onUpdateTable(table.id, { name: e.target.value || undefined })}
          />
        </label>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.5rem' }}>
          <label>
            Min seats
            <input
              type="number"
              min="1"
              value={table.minCapacity}
              onChange={e => onUpdateTable(table.id, { minCapacity: parseInt(e.target.value) || 1 })}
            />
          </label>
          <label>
            Max seats
            <input
              type="number"
              min="1"
              value={table.maxCapacity}
              onChange={e => onUpdateTable(table.id, { maxCapacity: parseInt(e.target.value) || 2 })}
            />
          </label>
        </div>
        <label>
          Shape
          <select
            value={table.shape}
            onChange={e => onUpdateTable(table.id, { shape: e.target.value as TableShape })}
          >
            <option value="Square">Square</option>
            <option value="Rectangle">Rectangle</option>
            <option value="Round">Round</option>
            <option value="Oval">Oval</option>
            <option value="Bar">Bar</option>
          </select>
        </label>
        <label>
          <input
            type="checkbox"
            checked={table.isCombinable}
            onChange={e => onUpdateTable(table.id, { isCombinable: e.target.checked })}
          />
          Can be merged
        </label>
        <hr />
        <p style={{ fontSize: '0.8rem', color: 'var(--pico-muted-color)' }}>
          Status: <span className={`badge ${table.status === 'Available' ? 'badge-success' : table.status === 'Occupied' ? 'badge-danger' : 'badge-warning'}`}>{table.status}</span>
        </p>
        <button className="secondary outline" onClick={() => onDeleteTable(table.id)} style={{ marginTop: '1rem', width: '100%' }}>
          Remove Table
        </button>
      </div>
    )
  }

  if (selected.kind === 'element') {
    const element = elements.find(e => e.id === selected.id)
    if (!element) return null

    return (
      <div className="floor-plan-properties">
        <h4>{element.type}</h4>
        <label>
          Label
          <input
            type="text"
            value={element.label ?? ''}
            placeholder="Optional label"
            onChange={e => onUpdateElement(element.id, { label: e.target.value || undefined })}
          />
        </label>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.5rem' }}>
          <label>
            Width
            <input
              type="number"
              min="10"
              value={element.width}
              onChange={e => onUpdateElement(element.id, { width: parseInt(e.target.value) || 10 })}
            />
          </label>
          <label>
            Height
            <input
              type="number"
              min="4"
              value={element.height}
              onChange={e => onUpdateElement(element.id, { height: parseInt(e.target.value) || 4 })}
            />
          </label>
        </div>
        <label>
          Rotation
          <input
            type="number"
            min="0" max="360" step="15"
            value={element.rotation}
            onChange={e => onUpdateElement(element.id, { rotation: parseInt(e.target.value) || 0 })}
          />
        </label>
        <button className="secondary outline" onClick={() => onDeleteElement(element.id)} style={{ marginTop: '1rem', width: '100%' }}>
          Remove Element
        </button>
      </div>
    )
  }

  return null
}

// ============================================================================
// Main Designer Page
// ============================================================================

export default function FloorPlanDesignerPage() {
  const { floorPlanId } = useParams<{ floorPlanId: string }>()
  const navigate = useNavigate()
  const { tables, fetchFloorPlan, fetchTablesForSite, createTableOnPlan, updateTablePosition, removeTableFromPlan, dispatch } = useBookings()

  const [floorPlan, setFloorPlan] = useState<FloorPlan | null>(null)
  const [selected, setSelected] = useState<SelectedItem>(null)
  const [dragState, setDragState] = useState<DragState | null>(null)
  const [nextTableNumber, setNextTableNumber] = useState(1)
  const svgRef = useRef<SVGSVGElement>(null)

  // Load floor plan and tables
  useEffect(() => {
    if (!floorPlanId) return
    fetchFloorPlan(floorPlanId).then(fp => {
      if (fp) setFloorPlan(fp)
    })
    fetchTablesForSite()
  }, [floorPlanId]) // eslint-disable-line react-hooks/exhaustive-deps

  // Compute next table number
  useEffect(() => {
    if (!floorPlan) return
    const planTables = tables.filter(t => floorPlan.tableIds.includes(t.id))
    const maxNum = planTables.reduce((max, t) => {
      const n = parseInt(t.number)
      return isNaN(n) ? max : Math.max(max, n)
    }, 0)
    setNextTableNumber(maxNum + 1)
  }, [tables, floorPlan])

  const planTables = floorPlan ? tables.filter(t => floorPlan.tableIds.includes(t.id)) : []
  const planElements = floorPlan?.elements ?? []

  // SVG coordinate helper
  const getSvgPoint = useCallback((e: React.MouseEvent): { x: number; y: number } => {
    const svg = svgRef.current
    if (!svg) return { x: 0, y: 0 }
    const pt = svg.createSVGPoint()
    pt.x = e.clientX
    pt.y = e.clientY
    const ctm = svg.getScreenCTM()
    if (!ctm) return { x: 0, y: 0 }
    const svgPt = pt.matrixTransform(ctm.inverse())
    return { x: svgPt.x, y: svgPt.y }
  }, [])

  // ---- Drag handlers ----

  const handleTableMouseDown = useCallback((tableId: string, e: React.MouseEvent) => {
    e.preventDefault()
    e.stopPropagation()
    setSelected({ kind: 'table', id: tableId })
    const pt = getSvgPoint(e)
    const table = tables.find(t => t.id === tableId)
    const pos = table?.position ?? { x: 50, y: 50 }
    setDragState({
      active: true,
      kind: 'table',
      id: tableId,
      offsetX: pt.x - pos.x,
      offsetY: pt.y - pos.y,
    })
  }, [tables, getSvgPoint])

  const handleElementMouseDown = useCallback((elementId: string, e: React.MouseEvent) => {
    e.preventDefault()
    e.stopPropagation()
    setSelected({ kind: 'element', id: elementId })
    const pt = getSvgPoint(e)
    const el = planElements.find(el => el.id === elementId)
    if (!el) return
    setDragState({
      active: true,
      kind: 'element',
      id: elementId,
      offsetX: pt.x - el.x,
      offsetY: pt.y - el.y,
    })
  }, [planElements, getSvgPoint])

  const handleMouseMove = useCallback((e: React.MouseEvent) => {
    if (!dragState?.active || !floorPlan) return
    const pt = getSvgPoint(e)
    const newX = snapToGrid(Math.max(0, Math.min(pt.x - dragState.offsetX, floorPlan.width - 40)))
    const newY = snapToGrid(Math.max(0, Math.min(pt.y - dragState.offsetY, floorPlan.height - 40)))

    if (dragState.kind === 'table') {
      const table = tables.find(t => t.id === dragState.id)
      if (table) {
        const defaults = TABLE_DEFAULTS[table.shape] ?? TABLE_DEFAULTS.Square
        const updated = {
          ...table,
          position: {
            x: newX,
            y: newY,
            width: table.position?.width ?? defaults.w,
            height: table.position?.height ?? defaults.h,
            rotation: table.position?.rotation,
          },
        }
        dispatch({ type: 'TABLE_UPDATED', payload: { table: updated } })
      }
    } else {
      // Element drag - update local state
      setFloorPlan(prev => {
        if (!prev) return prev
        return {
          ...prev,
          elements: prev.elements.map(el =>
            el.id === dragState.id ? { ...el, x: newX, y: newY } : el
          ),
        }
      })
    }
  }, [dragState, floorPlan, tables, getSvgPoint, dispatch])

  const handleMouseUp = useCallback(async () => {
    if (!dragState?.active || !floorPlan) {
      setDragState(null)
      return
    }

    if (dragState.kind === 'table') {
      const table = tables.find(t => t.id === dragState.id)
      if (table?.position) {
        await updateTablePosition(table.id, {
          x: table.position.x,
          y: table.position.y,
          width: table.position.width,
          height: table.position.height,
          rotation: table.position.rotation,
        })
      }
    } else {
      const el = floorPlan.elements.find(e => e.id === dragState.id)
      if (el) {
        await floorPlanApi.updateElement(floorPlan.id, el.id, { x: el.x, y: el.y })
      }
    }

    setDragState(null)
  }, [dragState, floorPlan, tables, updateTablePosition])

  // ---- Add handlers ----

  const handleAddTable = useCallback(async (shape: TableShape) => {
    if (!floorPlan) return
    const defaults = TABLE_DEFAULTS[shape]
    const num = String(nextTableNumber)
    const table = await createTableOnPlan(floorPlan.id, {
      number: num,
      shape,
      minCapacity: shape === 'Bar' ? 1 : 2,
      maxCapacity: defaults.seats,
    })
    if (table) {
      const centerX = snapToGrid((floorPlan.width - defaults.w) / 2)
      const centerY = snapToGrid((floorPlan.height - defaults.h) / 2)
      await updateTablePosition(table.id, {
        x: centerX,
        y: centerY,
        width: defaults.w,
        height: defaults.h,
      })
      // Reload floor plan to get updated tableIds
      const fp = await fetchFloorPlan(floorPlan.id)
      if (fp) setFloorPlan(fp)
      setSelected({ kind: 'table', id: table.id })
    }
  }, [floorPlan, nextTableNumber, createTableOnPlan, updateTablePosition, fetchFloorPlan])

  const handleAddElement = useCallback(async (type: FloorPlanElementType) => {
    if (!floorPlan) return
    const w = type === 'Wall' ? 160 : type === 'Door' ? 60 : 120
    const h = type === 'Wall' ? 10 : type === 'Door' ? 60 : 6
    const result = await floorPlanApi.addElement(floorPlan.id, {
      type,
      x: snapToGrid((floorPlan.width - w) / 2),
      y: snapToGrid((floorPlan.height - h) / 2),
      width: w,
      height: h,
    })
    // Reload floor plan
    const fp = await fetchFloorPlan(floorPlan.id)
    if (fp) setFloorPlan(fp)
    setSelected({ kind: 'element', id: result.id })
  }, [floorPlan, fetchFloorPlan])

  // ---- Update/delete handlers ----

  const handleUpdateTable = useCallback(async (tableId: string, updates: Parameters<typeof tableApi.updateTable>[1]) => {
    const table = await tableApi.updateTable(tableId, updates)
    dispatch({ type: 'TABLE_UPDATED', payload: { table } })
  }, [dispatch])

  const handleDeleteTable = useCallback(async (tableId: string) => {
    if (!floorPlan) return
    await removeTableFromPlan(floorPlan.id, tableId)
    const fp = await fetchFloorPlan(floorPlan.id)
    if (fp) setFloorPlan(fp)
    setSelected(null)
  }, [floorPlan, removeTableFromPlan, fetchFloorPlan])

  const handleUpdateElement = useCallback(async (elementId: string, updates: Parameters<typeof floorPlanApi.updateElement>[2]) => {
    if (!floorPlan) return
    await floorPlanApi.updateElement(floorPlan.id, elementId, updates)
    const fp = await fetchFloorPlan(floorPlan.id)
    if (fp) setFloorPlan(fp)
  }, [floorPlan, fetchFloorPlan])

  const handleDeleteElement = useCallback(async (elementId: string) => {
    if (!floorPlan) return
    await floorPlanApi.removeElement(floorPlan.id, elementId)
    const fp = await fetchFloorPlan(floorPlan.id)
    if (fp) setFloorPlan(fp)
    setSelected(null)
  }, [floorPlan, fetchFloorPlan])

  const handleCanvasClick = useCallback(() => {
    setSelected(null)
  }, [])

  // ---- Loading / error ----

  if (!floorPlan) {
    return (
      <>
        <hgroup>
          <h1>Floor Plan Designer</h1>
          <p>Loading...</p>
        </hgroup>
        <article aria-busy="true">Loading floor plan...</article>
      </>
    )
  }

  // ---- Render ----

  return (
    <>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '0.75rem' }}>
        <hgroup style={{ marginBottom: 0 }}>
          <h2 style={{ marginBottom: '0.15rem' }}>{floorPlan.name}</h2>
          <p style={{ color: 'var(--pico-muted-color)', fontSize: '0.85rem', marginBottom: 0 }}>
            {floorPlan.width} x {floorPlan.height} &middot; {planTables.length} table{planTables.length !== 1 ? 's' : ''}
          </p>
        </hgroup>
        <button className="secondary outline" onClick={() => navigate('/bookings/floor-plans')}>
          Back to Floor Plans
        </button>
      </div>

      <div className="floor-plan-designer">
        <Palette onAddTable={handleAddTable} onAddElement={handleAddElement} />

        <div className="floor-plan-canvas-wrapper">
          <svg
            ref={svgRef}
            className="floor-plan-svg"
            width={floorPlan.width}
            height={floorPlan.height}
            viewBox={`0 0 ${floorPlan.width} ${floorPlan.height}`}
            onMouseMove={handleMouseMove}
            onMouseUp={handleMouseUp}
            onMouseLeave={handleMouseUp}
            onClick={handleCanvasClick}
          >
            {/* Background */}
            <rect width={floorPlan.width} height={floorPlan.height} fill="white" rx="8" />

            {/* Grid dots */}
            <defs>
              <pattern id="grid-dots" width={GRID_SIZE} height={GRID_SIZE} patternUnits="userSpaceOnUse">
                <circle cx={GRID_SIZE / 2} cy={GRID_SIZE / 2} r="0.8" fill="#e0e0e0" />
              </pattern>
              <pattern id="grid-major" width={GRID_SIZE * 5} height={GRID_SIZE * 5} patternUnits="userSpaceOnUse">
                <circle cx={GRID_SIZE / 2} cy={GRID_SIZE / 2} r="1.2" fill="#bdbdbd" />
              </pattern>
            </defs>
            <rect width={floorPlan.width} height={floorPlan.height} fill="url(#grid-dots)" rx="8" />
            <rect width={floorPlan.width} height={floorPlan.height} fill="url(#grid-major)" rx="8" />

            {/* Structural elements (render below tables) */}
            {planElements.map(el => (
              <ElementSvg
                key={el.id}
                element={el}
                isSelected={selected?.kind === 'element' && selected.id === el.id}
                onMouseDown={e => handleElementMouseDown(el.id, e)}
              />
            ))}

            {/* Merge connectors */}
            <MergeConnectors tables={planTables} />

            {/* Tables */}
            {planTables.map(table => (
              <TableSvg
                key={table.id}
                table={table}
                isSelected={selected?.kind === 'table' && selected.id === table.id}
                onMouseDown={e => handleTableMouseDown(table.id, e)}
              />
            ))}
          </svg>
        </div>

        <PropertiesPanel
          selected={selected}
          tables={planTables}
          elements={planElements}
          floorPlan={floorPlan}
          onUpdateTable={handleUpdateTable}
          onDeleteTable={handleDeleteTable}
          onUpdateElement={handleUpdateElement}
          onDeleteElement={handleDeleteElement}
        />
      </div>
    </>
  )
}
