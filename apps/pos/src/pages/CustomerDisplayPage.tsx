import { useState } from 'react'
import { useCustomerDisplay, type PaymentMethodChoice, type ReceiptTypeChoice } from '../contexts/DisplayContext'
import type { Order } from '../types'

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-GB', {
    style: 'currency',
    currency: 'GBP',
  }).format(amount)
}

// --- Idle Screen ---
function IdleScreen() {
  return (
    <div className="display-screen idle-screen">
      <div className="idle-content">
        <h1>Welcome</h1>
        <p>Your order will appear here</p>
      </div>
    </div>
  )
}

// --- Order Screen ---
function OrderScreen({ order }: { order: Order }) {
  return (
    <div className="display-screen order-screen">
      <header className="display-header">
        <h2>Your Order</h2>
        <span className="display-order-number">#{order.orderNumber}</span>
      </header>

      <div className="display-order-items">
        {order.lines.map((line) => (
          <div key={line.id} className="display-order-line">
            <div className="display-line-left">
              <span className="display-line-qty">{line.quantity}x</span>
              <span className="display-line-name">{line.itemName}</span>
            </div>
            <span className="display-line-price">{formatCurrency(line.lineTotal)}</span>
          </div>
        ))}
      </div>

      <div className="display-totals">
        {order.discountTotal > 0 && (
          <div className="display-total-row discount-row">
            <span>Discounts</span>
            <span>-{formatCurrency(order.discountTotal)}</span>
          </div>
        )}
        <div className="display-total-row">
          <span>Subtotal</span>
          <span>{formatCurrency(order.subtotal)}</span>
        </div>
        {order.taxTotal > 0 && (
          <div className="display-total-row">
            <span>Tax</span>
            <span>{formatCurrency(order.taxTotal)}</span>
          </div>
        )}
        <div className="display-total-row grand-total">
          <span>Total</span>
          <span>{formatCurrency(order.grandTotal)}</span>
        </div>
      </div>
    </div>
  )
}

// --- Tip Screen ---
function TipScreen({ order, onSelect }: { order: Order; onSelect: (amount: number, percent: number) => void }) {
  const [customAmount, setCustomAmount] = useState('')
  const tipPresets = [0, 10, 15, 20]

  function handlePreset(percent: number) {
    const amount = Math.round(order.grandTotal * percent) / 100
    onSelect(amount, percent)
  }

  function handleCustom() {
    const amount = parseFloat(customAmount)
    if (!isNaN(amount) && amount >= 0) {
      const percent = order.grandTotal > 0 ? Math.round((amount / order.grandTotal) * 10000) / 100 : 0
      onSelect(amount, percent)
    }
  }

  function handleDigit(digit: string) {
    setCustomAmount((prev) => {
      if (digit === '.' && prev.includes('.')) return prev
      if (prev.includes('.') && prev.split('.')[1].length >= 2) return prev
      return prev + digit
    })
  }

  function handleClear() {
    setCustomAmount('')
  }

  return (
    <div className="display-screen tip-screen">
      <header className="display-header">
        <h2>Add a Tip?</h2>
        <p className="display-subtitle">Order Total: {formatCurrency(order.grandTotal)}</p>
      </header>

      <div className="tip-preset-buttons">
        {tipPresets.map((percent) => (
          <button
            key={percent}
            className="display-action-btn tip-btn"
            onClick={() => handlePreset(percent)}
          >
            <span className="tip-percent">{percent === 0 ? 'No Tip' : `${percent}%`}</span>
            {percent > 0 && (
              <span className="tip-amount">
                {formatCurrency(Math.round(order.grandTotal * percent) / 100)}
              </span>
            )}
          </button>
        ))}
      </div>

      <div className="tip-custom-section">
        <h3>Custom Amount</h3>
        <div className="tip-custom-display">
          {customAmount ? formatCurrency(parseFloat(customAmount) || 0) : formatCurrency(0)}
        </div>
        <div className="tip-custom-keypad">
          {['1', '2', '3', '4', '5', '6', '7', '8', '9', '.', '0'].map((digit) => (
            <button key={digit} onClick={() => handleDigit(digit)}>
              {digit}
            </button>
          ))}
          <button onClick={handleClear} className="secondary">&larr;</button>
        </div>
        <button
          className="display-action-btn"
          onClick={handleCustom}
          disabled={!customAmount || parseFloat(customAmount) <= 0}
        >
          Add {customAmount ? formatCurrency(parseFloat(customAmount) || 0) : ''} Tip
        </button>
      </div>
    </div>
  )
}

// --- Payment Method Screen ---
function PaymentMethodScreen({
  order,
  tipAmount,
  onSelect,
}: {
  order: Order
  tipAmount: number
  onSelect: (method: PaymentMethodChoice) => void
}) {
  const totalWithTip = order.grandTotal + tipAmount

  return (
    <div className="display-screen payment-method-screen">
      <header className="display-header">
        <h2>How would you like to pay?</h2>
        <div className="display-pay-total">
          <span className="display-total-label">Total Due</span>
          <span className="display-total-amount">{formatCurrency(totalWithTip)}</span>
          {tipAmount > 0 && (
            <span className="display-tip-included">
              (includes {formatCurrency(tipAmount)} tip)
            </span>
          )}
        </div>
      </header>

      <div className="payment-method-buttons">
        <button className="display-action-btn payment-cash-btn" onClick={() => onSelect('cash')}>
          <span className="payment-method-icon">Cash</span>
        </button>
        <button className="display-action-btn payment-card-btn" onClick={() => onSelect('card')}>
          <span className="payment-method-icon">Card</span>
        </button>
      </div>
    </div>
  )
}

// --- Processing Screen ---
function ProcessingScreen({ order, tipAmount }: { order: Order; tipAmount: number }) {
  const totalWithTip = order.grandTotal + tipAmount

  return (
    <div className="display-screen processing-screen">
      <div className="processing-content">
        <article aria-busy="true">
          <h2>Processing Payment</h2>
          <p>{formatCurrency(totalWithTip)}</p>
          <p className="processing-hint">Please wait...</p>
        </article>
      </div>
    </div>
  )
}

// --- Receipt Screen ---
function ReceiptScreen({ onSelect }: { onSelect: (type: ReceiptTypeChoice) => void }) {
  return (
    <div className="display-screen receipt-screen">
      <header className="display-header">
        <h2>How would you like your receipt?</h2>
      </header>

      <div className="receipt-buttons">
        <button className="display-action-btn receipt-btn" onClick={() => onSelect('print')}>
          <span className="receipt-icon">Printed Receipt</span>
        </button>
        <button className="display-action-btn receipt-btn" onClick={() => onSelect('email')}>
          <span className="receipt-icon">Email Receipt</span>
        </button>
        <button className="display-action-btn receipt-btn outline" onClick={() => onSelect('none')}>
          <span className="receipt-icon">No Receipt</span>
        </button>
      </div>
    </div>
  )
}

// --- Thank You Screen ---
function ThankYouScreen({
  totalPaid,
  changeAmount,
}: {
  totalPaid: number
  changeAmount: number
}) {
  return (
    <div className="display-screen thankyou-screen">
      <div className="thankyou-content">
        <h1>Thank You!</h1>
        <div className="thankyou-summary">
          <div className="display-total-row">
            <span>Paid</span>
            <span>{formatCurrency(totalPaid)}</span>
          </div>
          {changeAmount > 0 && (
            <div className="display-total-row change-row">
              <span>Change</span>
              <span>{formatCurrency(changeAmount)}</span>
            </div>
          )}
        </div>
        <p className="thankyou-message">Have a great day!</p>
      </div>
    </div>
  )
}

// --- Main Display Page ---
export default function CustomerDisplayPage() {
  const { screen, order, totalPaid, changeAmount, selectTip, selectPaymentMethod, selectReceiptType } =
    useCustomerDisplay()
  const [selectedTipAmount, setSelectedTipAmount] = useState(0)

  function handleTipSelected(tipAmount: number, tipPercent: number) {
    setSelectedTipAmount(tipAmount)
    selectTip(tipAmount, tipPercent)
  }

  function handlePaymentMethodSelected(method: PaymentMethodChoice) {
    selectPaymentMethod(method)
  }

  function handleReceiptTypeSelected(type: ReceiptTypeChoice) {
    selectReceiptType(type)
  }

  return (
    <main className="customer-display">
      {screen === 'idle' && <IdleScreen />}
      {screen === 'order' && order && <OrderScreen order={order} />}
      {screen === 'tip' && order && (
        <TipScreen order={order} onSelect={handleTipSelected} />
      )}
      {screen === 'payment' && order && (
        <PaymentMethodScreen
          order={order}
          tipAmount={selectedTipAmount}
          onSelect={handlePaymentMethodSelected}
        />
      )}
      {screen === 'processing' && order && (
        <ProcessingScreen order={order} tipAmount={selectedTipAmount} />
      )}
      {screen === 'receipt' && <ReceiptScreen onSelect={handleReceiptTypeSelected} />}
      {screen === 'thankyou' && (
        <ThankYouScreen totalPaid={totalPaid} changeAmount={changeAmount} />
      )}
    </main>
  )
}
