import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useOrder } from '../contexts/OrderContext'
import ActionsMenu from './ActionsMenu'
import DiscountModal from './DiscountModal'
import QuickPayModal from './QuickPayModal'
import HoldFireMenu from './HoldFireMenu'

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-GB', {
    style: 'currency',
    currency: 'GBP',
  }).format(amount)
}

export default function KeypadPanel() {
  const navigate = useNavigate()
  const {
    order,
    selectedLineId,
    keypadValue,
    setKeypadValue,
    changeQuantity,
    enterEditMode,
    sendOrder,
  } = useOrder()

  const [showActions, setShowActions] = useState(false)
  const [showDiscount, setShowDiscount] = useState(false)
  const [showHoldFire, setShowHoldFire] = useState(false)
  const [quickPayMethod, setQuickPayMethod] = useState<'cash' | 'card' | null>(null)

  function handleDigit(digit: string) {
    setKeypadValue(keypadValue + digit)
  }

  function handleClear() {
    setKeypadValue('')
  }

  function handleBackspace() {
    setKeypadValue(keypadValue.slice(0, -1))
  }

  function handleQuantity() {
    if (selectedLineId && keypadValue) {
      const qty = parseInt(keypadValue, 10)
      if (!isNaN(qty)) {
        changeQuantity(selectedLineId, qty)
      }
    }
    setKeypadValue('')
  }

  function handlePay() {
    if (order && order.lines.length > 0) {
      navigate('/payment')
    }
  }

  function handleSend() {
    if (order && unsentCount > 0) {
      sendOrder()
    }
  }

  const canPay = order && order.lines.length > 0
  const unsentCount = order?.lines.filter((line) => !line.sentAt).length ?? 0
  const heldCount = order?.lines.filter((line) => line.isHeld && !line.sentAt).length ?? 0

  return (
    <section className="keypad-panel">
      <div className="keypad-display" aria-live="polite">
        {keypadValue || '0'}
      </div>

      <p className="keypad-hint">Type a number below to add multiple items</p>

      <div className="keypad-grid">
        <button type="button" onClick={handleClear} className="secondary" aria-label="Clear">C</button>
        <button type="button" onClick={() => setShowHoldFire(true)} disabled={!order || unsentCount === 0} className={heldCount > 0 ? 'hold-btn' : ''}>
          Hold/Fire{heldCount > 0 ? ` (${heldCount})` : ''}
        </button>
        <button type="button" onClick={handleBackspace} className="secondary" aria-label="Backspace">&larr;</button>
        <button type="button" onClick={enterEditMode} disabled={!order || order.lines.length === 0}>Edit order</button>

        <button type="button" onClick={() => handleDigit('7')} aria-label="7">7</button>
        <button type="button" onClick={() => handleDigit('8')} aria-label="8">8</button>
        <button type="button" onClick={() => handleDigit('9')} aria-label="9">9</button>
        <button type="button" onClick={() => setShowDiscount(true)} disabled={!selectedLineId}>Discount</button>

        <button type="button" onClick={() => handleDigit('4')} aria-label="4">4</button>
        <button type="button" onClick={() => handleDigit('5')} aria-label="5">5</button>
        <button type="button" onClick={() => handleDigit('6')} aria-label="6">6</button>
        <button type="button" onClick={() => setShowActions(true)} disabled={!order}>Actions</button>

        <button type="button" onClick={() => handleDigit('1')} aria-label="1">1</button>
        <button type="button" onClick={() => handleDigit('2')} aria-label="2">2</button>
        <button type="button" onClick={() => handleDigit('3')} aria-label="3">3</button>
        <button type="button" onClick={() => setQuickPayMethod('cash')} className="cash-btn" disabled={!canPay}>Cash</button>

        <button type="button" onClick={() => handleDigit('00')} aria-label="00">00</button>
        <button type="button" onClick={() => handleDigit('0')} aria-label="0">0</button>
        <button type="button" onClick={handleQuantity} disabled={!selectedLineId || !keypadValue}>Qty</button>
        <button type="button" onClick={() => setQuickPayMethod('card')} className="quickpay-btn" disabled={!canPay}>Quickpay</button>
      </div>

      <div className="keypad-bottom-actions">
        <button
          type="button"
          onClick={handleSend}
          disabled={unsentCount === 0}
          className="send-btn"
        >
          Send{unsentCount > 0 ? ` - ${unsentCount} items` : ''}
        </button>
        <button
          type="button"
          onClick={handlePay}
          disabled={!canPay}
          className="checkout-btn"
        >
          Checkout{canPay ? ` - ${formatCurrency(order.grandTotal)}` : ''}
        </button>
      </div>

      {showActions && (
        <ActionsMenu onClose={() => setShowActions(false)} />
      )}

      {showDiscount && (
        <DiscountModal onClose={() => setShowDiscount(false)} />
      )}

      {showHoldFire && (
        <HoldFireMenu onClose={() => setShowHoldFire(false)} />
      )}

      {quickPayMethod && (
        <QuickPayModal
          method={quickPayMethod}
          onClose={() => setQuickPayMethod(null)}
        />
      )}
    </section>
  )
}
