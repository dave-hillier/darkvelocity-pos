import { useState } from 'react'
import { useOrder } from '../contexts/OrderContext'

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-GB', {
    style: 'currency',
    currency: 'GBP',
  }).format(amount)
}

interface DiscountModalProps {
  onClose: () => void
}

export default function DiscountModal({ onClose }: DiscountModalProps) {
  const { order, selectedLineId, applyDiscount } = useOrder()
  const [discountValue, setDiscountValue] = useState('')
  const [discountType, setDiscountType] = useState<'fixed' | 'percent'>('fixed')
  const [reason, setReason] = useState('')

  const selectedLine = order?.lines.find((line) => line.id === selectedLineId)

  if (!selectedLine) {
    onClose()
    return null
  }

  const lineSubtotal = selectedLine.quantity * selectedLine.unitPrice

  function handleDigit(digit: string) {
    setDiscountValue((prev) => prev + digit)
  }

  function handleClear() {
    setDiscountValue('')
  }

  function handleBackspace() {
    setDiscountValue((prev) => prev.slice(0, -1))
  }

  function handleApply() {
    const value = parseFloat(discountValue)
    if (isNaN(value) || value <= 0) {
      onClose()
      return
    }

    let discountAmount: number
    if (discountType === 'percent') {
      discountAmount = Math.min((value / 100) * lineSubtotal, lineSubtotal)
    } else {
      discountAmount = Math.min(value, lineSubtotal)
    }

    applyDiscount(selectedLineId!, discountAmount, reason || undefined)
    onClose()
  }

  const displayValue = discountType === 'percent'
    ? `${discountValue || '0'}%`
    : formatCurrency(parseFloat(discountValue) || 0)

  return (
    <dialog open aria-modal="true" className="discount-modal">
      <article>
        <header>
          <h3>Apply Discount</h3>
          <p>
            {selectedLine.itemName} - {formatCurrency(lineSubtotal)}
          </p>
        </header>

        <div className="discount-display" aria-live="polite">
          {displayValue}
        </div>

        <div className="discount-type-toggle">
          <button
            type="button"
            className={discountType === 'fixed' ? '' : 'outline'}
            onClick={() => setDiscountType('fixed')}
          >
            Fixed Amount
          </button>
          <button
            type="button"
            className={discountType === 'percent' ? '' : 'outline'}
            onClick={() => setDiscountType('percent')}
          >
            Percentage
          </button>
        </div>

        <div className="discount-keypad">
          {['7', '8', '9', '4', '5', '6', '1', '2', '3'].map((digit) => (
            <button
              key={digit}
              type="button"
              onClick={() => handleDigit(digit)}
            >
              {digit}
            </button>
          ))}
          <button type="button" onClick={handleClear} className="secondary">C</button>
          <button type="button" onClick={() => handleDigit('0')}>0</button>
          <button type="button" onClick={handleBackspace} className="secondary">&larr;</button>
        </div>

        <label>
          Reason (optional)
          <input
            type="text"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="e.g. Manager comp"
          />
        </label>

        <footer>
          <button type="button" className="secondary" onClick={onClose}>
            Cancel
          </button>
          <button type="button" onClick={handleApply} disabled={!discountValue}>
            Apply Discount
          </button>
        </footer>
      </article>
    </dialog>
  )
}
