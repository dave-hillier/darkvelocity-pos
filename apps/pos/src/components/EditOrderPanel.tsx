import { useOrder } from '../contexts/OrderContext'

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-GB', {
    style: 'currency',
    currency: 'GBP',
  }).format(amount)
}

export default function EditOrderPanel() {
  const {
    order,
    selectedLineIds,
    toggleLine,
    removeSelectedLines,
    exitEditMode,
  } = useOrder()

  if (!order) {
    return null
  }

  function handleRemove() {
    if (selectedLineIds.length > 0 && confirm(`Remove ${selectedLineIds.length} selected items?`)) {
      removeSelectedLines()
    }
  }

  function handleSelectAll() {
    order?.lines.forEach((line) => {
      if (!selectedLineIds.includes(line.id)) {
        toggleLine(line.id)
      }
    })
  }

  function handleDeselectAll() {
    selectedLineIds.forEach((id) => toggleLine(id))
  }

  const allSelected = order.lines.length > 0 && selectedLineIds.length === order.lines.length

  return (
    <section className="edit-order-panel">
      <header className="edit-order-header">
        <h2>Edit Order</h2>
        <div className="edit-order-actions">
          <button type="button" className="secondary outline" onClick={exitEditMode}>
            Cancel
          </button>
        </div>
      </header>

      <div className="edit-order-toolbar">
        <button
          type="button"
          className="outline"
          onClick={allSelected ? handleDeselectAll : handleSelectAll}
        >
          {allSelected ? 'Deselect All' : 'Select All'}
        </button>
        <span className="selected-count">
          {selectedLineIds.length} of {order.lines.length} selected
        </span>
      </div>

      <ul className="edit-order-lines" role="list">
        {order.lines.map((line) => {
          const isSelected = selectedLineIds.includes(line.id)
          return (
            <li
              key={line.id}
              className={`edit-order-line ${isSelected ? 'selected' : ''}`}
              onClick={() => toggleLine(line.id)}
              role="checkbox"
              aria-checked={isSelected}
              tabIndex={0}
            >
              <span className="checkbox" aria-hidden="true">
                {isSelected ? '\u2611' : '\u2610'}
              </span>
              <span className="line-qty">{line.quantity}x</span>
              <span className="line-name">{line.itemName}</span>
              {line.sentAt && <span className="sent-badge">Sent</span>}
              <span className="line-price">{formatCurrency(line.lineTotal)}</span>
            </li>
          )
        })}
      </ul>

      <footer className="edit-order-footer">
        <button
          type="button"
          className="secondary"
          onClick={handleRemove}
          disabled={selectedLineIds.length === 0}
        >
          Remove Selected ({selectedLineIds.length})
        </button>
        <button type="button" onClick={exitEditMode}>
          Done
        </button>
      </footer>
    </section>
  )
}
