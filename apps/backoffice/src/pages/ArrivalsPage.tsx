import { useState, useEffect, useCallback } from 'react'
import { useBookings } from '../contexts/BookingContext'
import { fetchDayView } from '../api/bookings'
import type { BookingStatus, DayView } from '../api/bookings'
import type { Table } from '../api/tables'

type StatusFilter = 'all' | 'expected' | 'arrived' | 'seated' | 'completed'

function getStatusBadge(status: BookingStatus) {
  switch (status) {
    case 'Confirmed':
      return { className: 'badge badge-success', label: 'Confirmed' }
    case 'Arrived':
      return { className: 'badge badge-warning', label: 'Arrived' }
    case 'Seated':
      return { className: 'badge badge-success', label: 'Seated' }
    case 'Completed':
      return { className: 'badge', label: 'Completed' }
    case 'Cancelled':
      return { className: 'badge badge-danger', label: 'Cancelled' }
    case 'NoShow':
      return { className: 'badge badge-danger', label: 'No Show' }
    case 'Requested':
      return { className: 'badge badge-warning', label: 'Requested' }
    case 'PendingDeposit':
      return { className: 'badge badge-warning', label: 'Pending Deposit' }
    default:
      return { className: 'badge', label: status }
  }
}

function matchesFilter(status: BookingStatus, filter: StatusFilter): boolean {
  switch (filter) {
    case 'all': return true
    case 'expected': return status === 'Confirmed' || status === 'Requested' || status === 'PendingDeposit'
    case 'arrived': return status === 'Arrived'
    case 'seated': return status === 'Seated'
    case 'completed': return status === 'Completed' || status === 'NoShow' || status === 'Cancelled'
  }
}

export default function ArrivalsPage() {
  const {
    bookings, tables, isLoading, error,
    fetchBookingsForDate, fetchTablesForSite,
    confirmBooking, checkinBooking, seatBooking, completeBooking, noShowBooking, cancelBooking,
  } = useBookings()

  const [filter, setFilter] = useState<StatusFilter>('all')
  const [dayView, setDayView] = useState<DayView | null>(null)
  const [seatDialog, setSeatDialog] = useState<{ bookingId: string; guestName: string; partySize: number } | null>(null)

  const loadData = useCallback(async () => {
    await Promise.all([
      fetchBookingsForDate(),
      fetchTablesForSite(),
    ])
    try {
      const dv = await fetchDayView()
      setDayView(dv)
    } catch {
      // day view is supplementary, don't block on failure
    }
  }, [fetchBookingsForDate, fetchTablesForSite])

  useEffect(() => {
    loadData()
    const interval = setInterval(loadData, 30000)
    return () => clearInterval(interval)
  }, [loadData])

  const filteredBookings = bookings.filter(b => matchesFilter(b.status, filter))

  // Group bookings by time slot (30-minute intervals)
  const grouped = new Map<string, typeof filteredBookings>()
  for (const booking of filteredBookings) {
    const time = booking.requestedTime
    // Parse time - could be HH:mm or ISO datetime
    let slotKey: string
    if (time.includes('T')) {
      const d = new Date(time)
      const h = d.getHours()
      const m = d.getMinutes() < 30 ? '00' : '30'
      slotKey = `${h.toString().padStart(2, '0')}:${m}`
    } else {
      const parts = time.split(':')
      const h = parts[0]
      const m = parseInt(parts[1]) < 30 ? '00' : '30'
      slotKey = `${h}:${m}`
    }
    if (!grouped.has(slotKey)) grouped.set(slotKey, [])
    grouped.get(slotKey)!.push(booking)
  }
  const sortedSlots = [...grouped.entries()].sort(([a], [b]) => a.localeCompare(b))

  const availableTables = tables.filter(t => t.status === 'Available' || t.status === 'Reserved')

  const handleSeat = async (tableId: string, table: Table) => {
    if (!seatDialog) return
    await seatBooking(seatDialog.bookingId, tableId, table.number, 'current-user')
    setSeatDialog(null)
    loadData()
  }

  const today = new Date()
  const dateStr = today.toLocaleDateString('en-GB', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })

  const stats = dayView ? {
    total: dayView.totalBookings,
    covers: dayView.totalCovers,
    confirmed: dayView.confirmedBookings,
    seated: dayView.seatedBookings,
    noShow: dayView.noShowCount,
    arrived: bookings.filter(b => b.status === 'Arrived').length,
  } : {
    total: bookings.length,
    covers: bookings.reduce((sum, b) => sum + b.partySize, 0),
    confirmed: bookings.filter(b => b.status === 'Confirmed').length,
    seated: bookings.filter(b => b.status === 'Seated').length,
    noShow: bookings.filter(b => b.status === 'NoShow').length,
    arrived: bookings.filter(b => b.status === 'Arrived').length,
  }

  return (
    <>
      <hgroup>
        <h1>Arrivals</h1>
        <p>{dateStr}</p>
      </hgroup>

      {error && (
        <article aria-label="Error">
          <p style={{ color: 'var(--pico-del-color)' }}>{error}</p>
        </article>
      )}

      {/* Stats summary */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(120px, 1fr))', gap: '0.75rem', marginBottom: '1.5rem' }}>
        <article style={{ textAlign: 'center', margin: 0, padding: '0.75rem' }}>
          <strong style={{ fontSize: '1.5rem' }}>{stats.total}</strong>
          <br /><small>Bookings</small>
        </article>
        <article style={{ textAlign: 'center', margin: 0, padding: '0.75rem' }}>
          <strong style={{ fontSize: '1.5rem' }}>{stats.covers}</strong>
          <br /><small>Covers</small>
        </article>
        <article style={{ textAlign: 'center', margin: 0, padding: '0.75rem' }}>
          <strong style={{ fontSize: '1.5rem' }}>{stats.confirmed}</strong>
          <br /><small>Expected</small>
        </article>
        <article style={{ textAlign: 'center', margin: 0, padding: '0.75rem' }}>
          <strong style={{ fontSize: '1.5rem' }}>{stats.arrived}</strong>
          <br /><small>Arrived</small>
        </article>
        <article style={{ textAlign: 'center', margin: 0, padding: '0.75rem' }}>
          <strong style={{ fontSize: '1.5rem' }}>{stats.seated}</strong>
          <br /><small>Seated</small>
        </article>
        <article style={{ textAlign: 'center', margin: 0, padding: '0.75rem' }}>
          <strong style={{ fontSize: '1.5rem', color: stats.noShow > 0 ? 'var(--pico-del-color)' : undefined }}>{stats.noShow}</strong>
          <br /><small>No-shows</small>
        </article>
      </div>

      {/* Filter tabs */}
      <nav style={{ marginBottom: '1rem' }}>
        <ul>
          {(['all', 'expected', 'arrived', 'seated', 'completed'] as StatusFilter[]).map(f => (
            <li key={f}>
              <a
                href="#"
                className={f === filter ? '' : 'secondary'}
                onClick={(e) => { e.preventDefault(); setFilter(f) }}
                style={{ textTransform: 'capitalize' }}
              >
                {f}
              </a>
            </li>
          ))}
          <li style={{ marginLeft: 'auto' }}>
            <button className="outline" onClick={loadData} aria-busy={isLoading}>
              Refresh
            </button>
          </li>
        </ul>
      </nav>

      {/* Table status summary */}
      {tables.length > 0 && (
        <details style={{ marginBottom: '1rem' }}>
          <summary>Tables ({tables.filter(t => t.status === 'Available').length} available / {tables.length} total)</summary>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem', padding: '0.5rem 0' }}>
            {tables.map(table => {
              let color = 'var(--pico-ins-color)'
              if (table.status === 'Occupied') color = 'var(--pico-del-color)'
              else if (table.status === 'Reserved') color = 'var(--pico-color-orange-550)'
              else if (table.status === 'Dirty') color = 'var(--pico-muted-color)'
              return (
                <span key={table.id} style={{
                  display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
                  width: '3rem', height: '3rem', borderRadius: '0.5rem',
                  border: `2px solid ${color}`, color,
                  fontWeight: 600, fontSize: '0.875rem',
                }}>
                  {table.number}
                </span>
              )
            })}
          </div>
        </details>
      )}

      {/* Timeline */}
      {isLoading && bookings.length === 0 ? (
        <article aria-busy="true">Loading bookings...</article>
      ) : sortedSlots.length === 0 ? (
        <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--pico-muted-color)' }}>
          No bookings match the current filter
        </p>
      ) : (
        sortedSlots.map(([slot, slotBookings]) => (
          <section key={slot} style={{ marginBottom: '1.5rem' }}>
            <hgroup style={{ marginBottom: '0.5rem' }}>
              <h3 style={{ margin: 0 }}>{slot}</h3>
              <p>{slotBookings.length} booking{slotBookings.length !== 1 ? 's' : ''} &middot; {slotBookings.reduce((s, b) => s + b.partySize, 0)} covers</p>
            </hgroup>

            {slotBookings.map(booking => {
              const badge = getStatusBadge(booking.status)
              const bookingTime = booking.requestedTime.includes('T')
                ? new Date(booking.requestedTime).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })
                : booking.requestedTime
              return (
                <article key={booking.id} style={{ margin: '0 0 0.5rem 0', padding: '0.75rem 1rem' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '1rem', flexWrap: 'wrap' }}>
                    <strong style={{ fontSize: '0.875rem', minWidth: '3rem' }}>{bookingTime}</strong>
                    <strong>{booking.guest.name}</strong>
                    <span>Party of {booking.partySize}</span>
                    <span className={badge.className}>{badge.label}</span>
                    {booking.specialRequests && (
                      <small style={{ color: 'var(--pico-muted-color)', fontStyle: 'italic' }}>{booking.specialRequests}</small>
                    )}
                    <div style={{ marginLeft: 'auto', display: 'flex', gap: '0.25rem', flexWrap: 'wrap' }}>
                      {booking.status === 'Requested' && (
                        <button
                          className="outline"
                          style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}
                          onClick={() => confirmBooking(booking.id)}
                        >
                          Confirm
                        </button>
                      )}
                      {booking.status === 'Confirmed' && (
                        <button
                          style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}
                          onClick={() => { checkinBooking(booking.id, 'current-user'); setTimeout(loadData, 500) }}
                        >
                          Check In
                        </button>
                      )}
                      {booking.status === 'Arrived' && (
                        <button
                          style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}
                          onClick={() => setSeatDialog({ bookingId: booking.id, guestName: booking.guest.name, partySize: booking.partySize })}
                        >
                          Seat
                        </button>
                      )}
                      {booking.status === 'Seated' && (
                        <button
                          className="outline"
                          style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}
                          onClick={() => { completeBooking(booking.id); setTimeout(loadData, 500) }}
                        >
                          Complete
                        </button>
                      )}
                      {(booking.status === 'Confirmed' || booking.status === 'Requested') && (
                        <button
                          className="secondary outline"
                          style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}
                          onClick={() => { noShowBooking(booking.id, 'current-user'); setTimeout(loadData, 500) }}
                        >
                          No Show
                        </button>
                      )}
                      {(booking.status === 'Requested' || booking.status === 'Confirmed' || booking.status === 'Arrived') && (
                        <button
                          className="secondary outline"
                          style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}
                          onClick={() => { cancelBooking(booking.id, 'Cancelled from arrivals', 'current-user'); setTimeout(loadData, 500) }}
                        >
                          Cancel
                        </button>
                      )}
                    </div>
                  </div>
                </article>
              )
            })}
          </section>
        ))
      )}

      {/* Seat dialog */}
      {seatDialog && (
        <dialog open>
          <article style={{ maxWidth: '500px' }}>
            <header>
              <button aria-label="Close" rel="prev" onClick={() => setSeatDialog(null)}></button>
              <h3>Seat {seatDialog.guestName}</h3>
            </header>
            <p>Party of {seatDialog.partySize} — select a table:</p>
            {availableTables.length === 0 ? (
              <p style={{ color: 'var(--pico-muted-color)' }}>No available tables</p>
            ) : (
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(100px, 1fr))', gap: '0.5rem' }}>
                {availableTables
                  .sort((a, b) => a.number.localeCompare(b.number, undefined, { numeric: true }))
                  .map(table => (
                  <button
                    key={table.id}
                    className={table.maxCapacity >= seatDialog.partySize ? '' : 'secondary outline'}
                    style={{ padding: '0.5rem', fontSize: '0.875rem' }}
                    onClick={() => handleSeat(table.id, table)}
                  >
                    <strong>{table.number}</strong>
                    <br />
                    <small>{table.minCapacity}-{table.maxCapacity} pax</small>
                  </button>
                ))}
              </div>
            )}
            <footer>
              <button className="secondary" onClick={() => setSeatDialog(null)}>Cancel</button>
            </footer>
          </article>
        </dialog>
      )}
    </>
  )
}
