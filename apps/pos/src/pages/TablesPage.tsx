import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { fetchDayView } from '../api/bookings'
import type { DayView, BookingReference, BookingStatus } from '../api/bookings'

function statusLabel(status: BookingStatus): string {
  switch (status) {
    case 'Confirmed': return 'Expected'
    case 'Arrived': return 'Arrived'
    case 'Seated': return 'Seated'
    case 'Completed': return 'Done'
    case 'NoShow': return 'No Show'
    case 'Cancelled': return 'Cancelled'
    case 'Requested': return 'Pending'
    case 'PendingDeposit': return 'Deposit'
    default: return status
  }
}

function statusColor(status: BookingStatus): string {
  switch (status) {
    case 'Confirmed':
    case 'Requested':
    case 'PendingDeposit':
      return 'var(--pico-primary)'
    case 'Arrived':
      return 'var(--pico-color-orange-550)'
    case 'Seated':
      return 'var(--pico-ins-color)'
    case 'Completed':
      return 'var(--pico-muted-color)'
    case 'NoShow':
    case 'Cancelled':
      return 'var(--pico-del-color)'
    default:
      return 'var(--pico-muted-color)'
  }
}

export default function TablesPage() {
  const navigate = useNavigate()
  const [dayView, setDayView] = useState<DayView | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let mounted = true
    async function load() {
      try {
        const dv = await fetchDayView()
        if (mounted) {
          setDayView(dv)
          setError(null)
        }
      } catch (err) {
        if (mounted) setError((err as Error).message)
      } finally {
        if (mounted) setLoading(false)
      }
    }
    load()
    const interval = setInterval(load, 30000)
    return () => { mounted = false; clearInterval(interval) }
  }, [])

  // Flatten all bookings and sort by time
  const allBookings: BookingReference[] = dayView?.slots.flatMap(s => s.bookings) ?? []
  const activeBookings = allBookings.filter(b =>
    b.status !== 'Completed' && b.status !== 'Cancelled'
  )

  return (
    <div style={{ padding: '1rem', maxWidth: '600px', margin: '0 auto' }}>
      <header style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
        <hgroup style={{ margin: 0 }}>
          <h2 style={{ margin: 0 }}>Today's Bookings</h2>
          {dayView && (
            <p style={{ margin: 0 }}>{dayView.totalBookings} bookings &middot; {dayView.totalCovers} covers</p>
          )}
        </hgroup>
        <button className="secondary outline" style={{ padding: '0.25rem 0.75rem' }} onClick={() => navigate('/register')}>
          Back
        </button>
      </header>

      {error && (
        <article aria-label="Error">
          <p style={{ color: 'var(--pico-del-color)' }}>{error}</p>
        </article>
      )}

      {loading ? (
        <article aria-busy="true">Loading...</article>
      ) : activeBookings.length === 0 ? (
        <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--pico-muted-color)' }}>
          No active bookings today
        </p>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Time</th>
              <th>Guest</th>
              <th>Pax</th>
              <th>Table</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            {activeBookings.map(booking => (
              <tr key={booking.bookingId}>
                <td><strong>{booking.time}</strong></td>
                <td>{booking.guestName}</td>
                <td>{booking.partySize}</td>
                <td>{booking.tableNumber ?? '—'}</td>
                <td>
                  <small style={{ color: statusColor(booking.status), fontWeight: 600 }}>
                    {statusLabel(booking.status)}
                  </small>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}
