import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useBookings } from '../contexts/BookingContext'

export default function FloorPlansPage() {
  const { floorPlans, tables, isLoading, error, createFloorPlan } = useBookings()
  const navigate = useNavigate()
  const [showCreate, setShowCreate] = useState(false)
  const [newName, setNewName] = useState('')
  const [newWidth, setNewWidth] = useState(800)
  const [newHeight, setNewHeight] = useState(600)
  const [creating, setCreating] = useState(false)

  async function handleCreate() {
    if (!newName.trim()) return
    setCreating(true)
    const id = await createFloorPlan({
      name: newName.trim(),
      width: newWidth,
      height: newHeight,
    })
    setCreating(false)
    if (id) {
      navigate(`/bookings/floor-plans/${id}`)
    }
  }

  return (
    <>
      <hgroup>
        <h1>Floor Plans</h1>
        <p>Manage dining room layouts and table assignments</p>
      </hgroup>

      {error && (
        <article aria-label="Error">
          <p style={{ color: 'var(--pico-del-color)' }}>{error}</p>
        </article>
      )}

      <div style={{ marginBottom: '1rem', display: 'flex', justifyContent: 'flex-end' }}>
        <button onClick={() => setShowCreate(!showCreate)}>
          {showCreate ? 'Cancel' : 'Add Floor Plan'}
        </button>
      </div>

      {showCreate && (
        <article>
          <header>
            <h3>New Floor Plan</h3>
          </header>
          <label>
            Name
            <input
              type="text"
              value={newName}
              onChange={e => setNewName(e.target.value)}
              placeholder="e.g. Main Dining Room"
              autoFocus
            />
          </label>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem' }}>
            <label>
              Width (px)
              <input
                type="number"
                min="400"
                max="2000"
                value={newWidth}
                onChange={e => setNewWidth(parseInt(e.target.value) || 800)}
              />
            </label>
            <label>
              Height (px)
              <input
                type="number"
                min="300"
                max="1500"
                value={newHeight}
                onChange={e => setNewHeight(parseInt(e.target.value) || 600)}
              />
            </label>
          </div>
          <button onClick={handleCreate} disabled={creating || !newName.trim()} aria-busy={creating}>
            {creating ? 'Creating...' : 'Create & Open Designer'}
          </button>
        </article>
      )}

      {floorPlans.length === 0 && !isLoading && !showCreate && (
        <article style={{ textAlign: 'center', padding: '3rem' }}>
          <p style={{ color: 'var(--pico-muted-color)', marginBottom: '1rem' }}>
            No floor plans configured yet
          </p>
          <button onClick={() => setShowCreate(true)}>Create Your First Floor Plan</button>
        </article>
      )}

      {floorPlans.map((plan) => (
        <article key={plan.id} aria-busy={isLoading}>
          <header style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
            <hgroup>
              <h3>{plan.name}</h3>
              <p>
                {plan.width} x {plan.height}
                {plan.isDefault && <span className="badge badge-success" style={{ marginLeft: '0.5rem' }}>Default</span>}
                {!plan.isActive && <span className="badge badge-danger" style={{ marginLeft: '0.5rem' }}>Inactive</span>}
              </p>
            </hgroup>
            <button onClick={() => navigate(`/bookings/floor-plans/${plan.id}`)}>
              Open Designer
            </button>
          </header>

          <h4>Tables ({plan.tableIds.length})</h4>
          {plan.tableIds.length > 0 ? (
            <table>
              <thead>
                <tr>
                  <th>Number</th>
                  <th>Name</th>
                  <th>Shape</th>
                  <th>Capacity</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {plan.tableIds.map((tableId) => {
                  const t = tables.find((tbl) => tbl.id === tableId)
                  if (!t) return null
                  return (
                    <tr key={tableId}>
                      <td>{t.number}</td>
                      <td>{t.name ?? '-'}</td>
                      <td>{t.shape}</td>
                      <td>{t.minCapacity}-{t.maxCapacity}</td>
                      <td>
                        <span className={
                          t.status === 'Available' ? 'badge badge-success' :
                          t.status === 'Occupied' ? 'badge badge-danger' :
                          'badge badge-warning'
                        }>
                          {t.status}
                        </span>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          ) : (
            <p style={{ color: 'var(--pico-muted-color)' }}>No tables assigned — open the designer to add tables</p>
          )}

          {plan.sections.length > 0 && (
            <>
              <h4>Sections</h4>
              <ul>
                {plan.sections.map((section) => (
                  <li key={section.id}>
                    <strong>{section.name}</strong> - {section.tableIds.length} table{section.tableIds.length !== 1 ? 's' : ''}
                  </li>
                ))}
              </ul>
            </>
          )}
        </article>
      ))}
    </>
  )
}
