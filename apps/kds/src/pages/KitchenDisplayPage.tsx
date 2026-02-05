import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useStation } from '../contexts/StationContext'
import { useDeviceAuth } from '../contexts/DeviceAuthContext'

interface OrderItem {
  id: string
  name: string
  quantity: number
  modifiers: string[]
}

interface KitchenOrder {
  id: string
  orderNumber: string
  orderType: string
  tableNumber?: string
  items: OrderItem[]
  status: 'pending' | 'cooking' | 'ready'
  createdAt: Date
  startedAt?: Date
}

// Mock orders for demo
const mockOrders: KitchenOrder[] = [
  {
    id: '1',
    orderNumber: '#1042',
    orderType: 'Dine In',
    tableNumber: 'T5',
    items: [
      { id: '1a', name: 'Grilled Salmon', quantity: 1, modifiers: ['No butter', 'Extra lemon'] },
      { id: '1b', name: 'Caesar Salad', quantity: 1, modifiers: ['Dressing on side'] },
    ],
    status: 'cooking',
    createdAt: new Date(Date.now() - 8 * 60000),
    startedAt: new Date(Date.now() - 5 * 60000),
  },
  {
    id: '2',
    orderNumber: '#1043',
    orderType: 'Takeout',
    items: [
      { id: '2a', name: 'Chicken Burger', quantity: 2, modifiers: ['No pickles'] },
      { id: '2b', name: 'Fries', quantity: 2, modifiers: ['Extra crispy'] },
    ],
    status: 'pending',
    createdAt: new Date(Date.now() - 3 * 60000),
  },
  {
    id: '3',
    orderNumber: '#1044',
    orderType: 'Dine In',
    tableNumber: 'T12',
    items: [
      { id: '3a', name: 'Ribeye Steak', quantity: 1, modifiers: ['Medium rare', 'Mushroom sauce'] },
      { id: '3b', name: 'Mashed Potatoes', quantity: 1, modifiers: [] },
      { id: '3c', name: 'Grilled Asparagus', quantity: 1, modifiers: [] },
    ],
    status: 'pending',
    createdAt: new Date(Date.now() - 1 * 60000),
  },
  {
    id: '4',
    orderNumber: '#1041',
    orderType: 'Dine In',
    tableNumber: 'T3',
    items: [
      { id: '4a', name: 'Fish & Chips', quantity: 1, modifiers: [] },
    ],
    status: 'ready',
    createdAt: new Date(Date.now() - 15 * 60000),
    startedAt: new Date(Date.now() - 12 * 60000),
  },
]

export default function KitchenDisplayPage() {
  const { selectedStation, clearStation } = useStation()
  const { isDeviceAuthenticated } = useDeviceAuth()
  const [orders, setOrders] = useState<KitchenOrder[]>(mockOrders)
  const [, setTick] = useState(0)
  const navigate = useNavigate()

  useEffect(() => {
    if (!isDeviceAuthenticated) {
      navigate('/setup', { replace: true })
    } else if (!selectedStation) {
      navigate('/station', { replace: true })
    }
  }, [isDeviceAuthenticated, selectedStation, navigate])

  // Update timer every second
  useEffect(() => {
    const timer = setInterval(() => setTick(t => t + 1), 1000)
    return () => clearInterval(timer)
  }, [])

  function getElapsedTime(date: Date): string {
    const seconds = Math.floor((Date.now() - date.getTime()) / 1000)
    const mins = Math.floor(seconds / 60)
    const secs = seconds % 60
    return `${mins}:${secs.toString().padStart(2, '0')}`
  }

  function getStatusColor(order: KitchenOrder): string {
    const elapsed = (Date.now() - order.createdAt.getTime()) / 60000
    if (elapsed > 15) return 'urgent'
    return order.status
  }

  function handleBump(orderId: string) {
    setOrders(prev =>
      prev.map(order => {
        if (order.id !== orderId) return order
        if (order.status === 'pending') {
          return { ...order, status: 'cooking' as const, startedAt: new Date() }
        }
        if (order.status === 'cooking') {
          return { ...order, status: 'ready' as const }
        }
        return order
      }).filter(order => !(order.id === orderId && order.status === 'ready'))
    )
  }

  function handleChangeStation() {
    clearStation()
    navigate('/station', { replace: true })
  }

  if (!selectedStation) {
    return null
  }

  return (
    <div style={{ height: '100vh', display: 'flex', flexDirection: 'column' }}>
      {/* Header */}
      <header style={{
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: '0.75rem 1rem',
        backgroundColor: 'var(--pico-card-background-color)',
        borderBottom: '1px solid var(--pico-muted-border-color)',
      }}>
        <div>
          <strong style={{ fontSize: '1.25rem' }}>{selectedStation.name}</strong>
          <span style={{ marginLeft: '1rem', color: 'var(--pico-muted-color)' }}>
            {orders.filter(o => o.status !== 'ready').length} active orders
          </span>
        </div>
        <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
          <span style={{ color: 'var(--pico-muted-color)', fontSize: '0.875rem' }}>
            {new Date().toLocaleTimeString()}
          </span>
          <button className="outline" style={{ padding: '0.25rem 0.75rem' }} onClick={handleChangeStation}>
            Change Station
          </button>
        </div>
      </header>

      {/* Orders Grid */}
      <div className="kds-grid">
        {orders.map(order => (
          <article
            key={order.id}
            className={`order-card ${getStatusColor(order)}`}
            onClick={() => handleBump(order.id)}
            style={{ cursor: 'pointer' }}
          >
            <div className="order-header">
              <div>
                <strong>{order.orderNumber}</strong>
                <span style={{ marginLeft: '0.5rem', fontSize: '0.875rem', color: 'var(--pico-muted-color)' }}>
                  {order.orderType}
                  {order.tableNumber && ` - ${order.tableNumber}`}
                </span>
              </div>
              <div className="order-timer">
                {getElapsedTime(order.createdAt)}
              </div>
            </div>

            <div className="order-items">
              {order.items.map(item => (
                <div key={item.id}>
                  <div className="order-item">
                    <span>
                      <span className="item-qty">{item.quantity}x</span>
                      {item.name}
                    </span>
                  </div>
                  {item.modifiers.length > 0 && (
                    <div className="item-mods">
                      {item.modifiers.join(', ')}
                    </div>
                  )}
                </div>
              ))}
            </div>

            <footer style={{
              padding: '0.5rem 1rem',
              backgroundColor: 'var(--pico-card-background-color)',
              textAlign: 'center',
              fontSize: '0.875rem',
              textTransform: 'uppercase',
              fontWeight: 600,
            }}>
              {order.status === 'pending' && 'Tap to Start'}
              {order.status === 'cooking' && 'Tap when Ready'}
              {order.status === 'ready' && 'Tap to Clear'}
            </footer>
          </article>
        ))}

        {orders.length === 0 && (
          <div style={{
            gridColumn: '1 / -1',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            height: '50vh',
          }}>
            <p style={{ fontSize: '1.5rem', color: 'var(--pico-muted-color)' }}>
              No orders to display
            </p>
          </div>
        )}
      </div>

      {/* Legend */}
      <footer style={{
        display: 'flex',
        justifyContent: 'center',
        gap: '2rem',
        padding: '0.5rem',
        backgroundColor: 'var(--pico-card-background-color)',
        borderTop: '1px solid var(--pico-muted-border-color)',
        fontSize: '0.875rem',
      }}>
        <span><span style={{ color: 'var(--kds-pending)' }}>■</span> Pending</span>
        <span><span style={{ color: 'var(--kds-cooking)' }}>■</span> Cooking</span>
        <span><span style={{ color: 'var(--kds-ready)' }}>■</span> Ready</span>
        <span><span style={{ color: 'var(--kds-urgent)' }}>■</span> Urgent (15+ min)</span>
      </footer>
    </div>
  )
}
