import { useOrder } from '../contexts/OrderContext'
import { useAuth } from '../contexts/AuthContext'

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-GB', {
    style: 'currency',
    currency: 'GBP',
  }).format(amount)
}

export default function OrderPanel() {
  const { order, selectedLineId, selectLine, removeItem } = useOrder()
  const { user, logout } = useAuth()

  if (!order) {
    return (
      <section className="order-panel">
        <header>
          <h2>New Order</h2>
          <p>Select an item from the menu to start</p>
        </header>
        <footer style={{ marginTop: 'auto', paddingTop: '1rem' }}>
          <small>Logged in as {user?.firstName} {user?.lastName}</small>
          <button className="secondary outline" onClick={logout}>
            Log Out
          </button>
        </footer>
      </section>
    )
  }

  return (
    <section className="order-panel">
      <header>
        <h2>Order #{order.orderNumber}</h2>
      </header>

      <ul className="order-lines" role="list">
        {order.lines.map((line) => (
          <li
            key={line.id}
            className={`order-line ${selectedLineId === line.id ? 'selected' : ''}`}
            onClick={() => selectLine(selectedLineId === line.id ? null : line.id)}
            role="button"
            tabIndex={0}
            aria-selected={selectedLineId === line.id}
          >
            <div className="order-line-info">
              <span className="order-line-qty">{line.quantity}x</span>
              <span className="order-line-name">{line.itemName}</span>
              {line.discountAmount > 0 && (
                <small style={{ display: 'block', color: 'var(--pico-del-color)' }}>
                  Discount: -{formatCurrency(line.discountAmount)}
                </small>
              )}
            </div>
            <div className="order-line-price">
              {formatCurrency(line.lineTotal)}
            </div>
          </li>
        ))}
      </ul>

      {order.lines.length === 0 && (
        <p style={{ textAlign: 'center', color: 'var(--pico-muted-color)' }}>
          No items yet
        </p>
      )}

      <div className="order-totals">
        <div className="order-total-row">
          <span>Subtotal</span>
          <span>{formatCurrency(order.subtotal)}</span>
        </div>
        {order.discountTotal > 0 && (
          <div className="order-total-row" style={{ color: 'var(--pico-del-color)' }}>
            <span>Discounts</span>
            <span>-{formatCurrency(order.discountTotal)}</span>
          </div>
        )}
        <div className="order-total-row">
          <span>Tax (VAT)</span>
          <span>{formatCurrency(order.taxTotal)}</span>
        </div>
        <div className="order-total-row grand-total">
          <span>Total</span>
          <span>{formatCurrency(order.grandTotal)}</span>
        </div>
      </div>

      {selectedLineId && (
        <div style={{ padding: '0.5rem' }}>
          <button
            className="secondary"
            onClick={() => removeItem(selectedLineId)}
          >
            Remove Selected Item
          </button>
        </div>
      )}

      <footer style={{ marginTop: '1rem' }}>
        <small>Logged in as {user?.firstName} {user?.lastName}</small>
        <button className="secondary outline" onClick={logout}>
          Log Out
        </button>
      </footer>
    </section>
  )
}
