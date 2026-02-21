import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useOrder } from '../contexts/OrderContext'
import { useRegisterDisplay, type PaymentMethodChoice, type ReceiptTypeChoice } from '../contexts/DisplayContext'

type PaymentMethod = 'cash' | 'card'

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-GB', {
    style: 'currency',
    currency: 'GBP',
  }).format(amount)
}

const quickAmounts = [5, 10, 20, 50]

export default function PaymentPage() {
  const navigate = useNavigate()
  const { order, completePayment, clearOrder } = useOrder()
  const display = useRegisterDisplay()
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod | null>(null)
  const [amountReceived, setAmountReceived] = useState('')
  const [tipAmount, setTipAmount] = useState(0)
  const [isProcessing, setIsProcessing] = useState(false)
  const [paymentComplete, setPaymentComplete] = useState(false)
  const [receiptType, setReceiptType] = useState<ReceiptTypeChoice | null>(null)
  const [awaitingReceipt, setAwaitingReceipt] = useState(false)

  if (!order || order.lines.length === 0) {
    navigate('/register')
    return null
  }

  const orderGrandTotal = order.grandTotal
  const totalWithTip = orderGrandTotal + tipAmount
  const receivedAmount = parseFloat(amountReceived) || 0
  const changeAmount = Math.max(0, receivedAmount - totalWithTip)
  const canComplete = paymentMethod === 'card' || receivedAmount >= totalWithTip

  // Send tip request to display on mount
  useEffect(() => {
    if (display.isDisplayConnected && order) {
      display.requestTip(order)
    }
  }, []) // eslint-disable-line react-hooks/exhaustive-deps

  // Listen for tip selection from display
  useEffect(() => {
    display.onTipSelected((amount: number) => {
      setTipAmount(amount)
      // After tip is selected, request payment method from display
      if (order) {
        display.requestPayment(order)
      }
    })
  }, [display, order])

  // Listen for payment method selection from display
  useEffect(() => {
    display.onPaymentMethodSelected((method: PaymentMethodChoice) => {
      setPaymentMethod(method)
    })
  }, [display])

  // Listen for receipt type selection from display
  useEffect(() => {
    display.onReceiptTypeSelected((type: ReceiptTypeChoice) => {
      setReceiptType(type)
      setAwaitingReceipt(false)
      // Show thank you then go idle
      if (order) {
        display.sendPaymentComplete(order, totalWithTip, changeAmount)
        setTimeout(() => display.sendIdle(), 5000)
      }
    })
  }, [display, order, totalWithTip, changeAmount])

  function handleDigit(digit: string) {
    setAmountReceived((prev) => {
      if (digit === '.' && prev.includes('.')) return prev
      if (prev.includes('.') && prev.split('.')[1].length >= 2) return prev
      return prev + digit
    })
  }

  function handleClear() {
    setAmountReceived('')
  }

  function handleBackspace() {
    setAmountReceived((prev) => prev.slice(0, -1))
  }

  function handleQuickAmount(amount: number) {
    setAmountReceived(amount.toFixed(2))
  }

  function handleExactAmount() {
    setAmountReceived(totalWithTip.toFixed(2))
  }

  function handleTipPreset(percent: number) {
    const amount = Math.round(orderGrandTotal * percent) / 100
    setTipAmount(amount)
  }

  async function handleCompletePayment() {
    if (!canComplete) return

    setIsProcessing(true)

    // Send processing state to display
    if (display.isDisplayConnected && order) {
      display.sendPaymentProcessing(order)
    }

    try {
      // Simulate payment processing
      await new Promise((resolve) => setTimeout(resolve, 1000))

      completePayment('payment-' + Date.now())
      setPaymentComplete(true)

      // Request receipt type from display
      if (display.isDisplayConnected && order) {
        display.requestReceipt(order, totalWithTip, changeAmount)
        setAwaitingReceipt(true)
      }
    } catch (error) {
      console.error('Payment failed:', error)
      alert('Payment failed. Please try again.')
      // Revert display to payment method screen
      if (display.isDisplayConnected && order) {
        display.requestPayment(order)
      }
    } finally {
      setIsProcessing(false)
    }
  }

  function handleNewOrder() {
    clearOrder()
    display.sendIdle()
    navigate('/register')
  }

  function handleBack() {
    display.sendIdle()
    navigate('/register')
  }

  if (paymentComplete) {
    return (
      <main className="payment-complete">
        <article>
          <header>
            <h1>Payment Complete</h1>
          </header>
          <div className="payment-summary">
            <div className="summary-row">
              <span>Total</span>
              <span>{formatCurrency(totalWithTip)}</span>
            </div>
            {tipAmount > 0 && (
              <div className="summary-row">
                <span>Tip</span>
                <span>{formatCurrency(tipAmount)}</span>
              </div>
            )}
            {paymentMethod === 'cash' && (
              <>
                <div className="summary-row">
                  <span>Received</span>
                  <span>{formatCurrency(receivedAmount)}</span>
                </div>
                <div className="summary-row change">
                  <span>Change</span>
                  <span>{formatCurrency(changeAmount)}</span>
                </div>
              </>
            )}
            {receiptType && (
              <div className="summary-row">
                <span>Receipt</span>
                <span>{receiptType === 'print' ? 'Printed' : receiptType === 'email' ? 'Emailed' : 'None'}</span>
              </div>
            )}
          </div>
          {awaitingReceipt && display.isDisplayConnected && (
            <p aria-busy="true">Waiting for customer to select receipt type...</p>
          )}
          <footer>
            <button onClick={handleNewOrder}>
              New Order
            </button>
          </footer>
        </article>
      </main>
    )
  }

  return (
    <main className="payment-page">
      <header className="payment-header">
        <button className="secondary outline" onClick={handleBack}>
          Back
        </button>
        <h1>Payment</h1>
        <div className="payment-total">
          {formatCurrency(totalWithTip)}
        </div>
      </header>

      {display.isDisplayConnected && !paymentMethod && (
        <article>
          <p aria-busy="true">
            Customer is selecting tip and payment method on display...
          </p>
        </article>
      )}

      {!paymentMethod && (
        <section className="payment-methods">
          <h2>Select Payment Method</h2>
          <div className="method-buttons">
            <button
              className="payment-method-btn"
              onClick={() => {
                setPaymentMethod('cash')
                if (display.isDisplayConnected && order) {
                  display.sendPaymentProcessing(order)
                }
              }}
            >
              <span className="method-icon">Cash</span>
            </button>
            <button
              className="payment-method-btn"
              onClick={() => {
                setPaymentMethod('card')
                if (display.isDisplayConnected && order) {
                  display.sendPaymentProcessing(order)
                }
              }}
            >
              <span className="method-icon">Card</span>
            </button>
          </div>
        </section>
      )}

      {paymentMethod === 'cash' && (
        <section className="cash-payment">
          <h2>Cash Payment</h2>

          <div className="tip-selection">
            <h3>Add Tip</h3>
            <div className="tip-presets">
              {[0, 10, 15, 20].map((percent) => (
                <button
                  key={percent}
                  className={tipAmount === Math.round(orderGrandTotal * percent) / 100 ? '' : 'outline'}
                  onClick={() => handleTipPreset(percent)}
                >
                  {percent === 0 ? 'No Tip' : `${percent}%`}
                </button>
              ))}
            </div>
            {tipAmount > 0 && (
              <p>Tip: {formatCurrency(tipAmount)}</p>
            )}
          </div>

          <div className="amount-display">
            <label>Amount Received</label>
            <div className="amount-value">
              {amountReceived ? formatCurrency(parseFloat(amountReceived)) : formatCurrency(0)}
            </div>
            {receivedAmount >= totalWithTip && (
              <div className="change-display">
                Change: {formatCurrency(changeAmount)}
              </div>
            )}
          </div>

          <div className="quick-amounts">
            {quickAmounts.map((amount) => (
              <button
                key={amount}
                className="outline"
                onClick={() => handleQuickAmount(amount)}
              >
                {formatCurrency(amount)}
              </button>
            ))}
            <button className="outline" onClick={handleExactAmount}>
              Exact
            </button>
          </div>

          <div className="pin-keypad">
            {['1', '2', '3', '4', '5', '6', '7', '8', '9', '.', '0'].map((digit) => (
              <button
                key={digit}
                type="button"
                onClick={() => handleDigit(digit)}
              >
                {digit}
              </button>
            ))}
            <button type="button" onClick={handleBackspace} className="secondary">
              &larr;
            </button>
          </div>

          <div className="payment-actions">
            <button className="secondary" onClick={handleClear}>
              Clear
            </button>
            <button
              className="secondary outline"
              onClick={() => setPaymentMethod(null)}
            >
              Change Method
            </button>
          </div>
        </section>
      )}

      {paymentMethod === 'card' && (
        <section className="card-payment">
          <h2>Card Payment</h2>

          <div className="tip-selection">
            <h3>Add Tip</h3>
            <div className="tip-presets">
              {[0, 10, 15, 20].map((percent) => (
                <button
                  key={percent}
                  className={tipAmount === Math.round(orderGrandTotal * percent) / 100 ? '' : 'outline'}
                  onClick={() => handleTipPreset(percent)}
                >
                  {percent === 0 ? 'No Tip' : `${percent}%`}
                </button>
              ))}
            </div>
            {tipAmount > 0 && (
              <p>Tip: {formatCurrency(tipAmount)}</p>
            )}
          </div>

          <article aria-busy={isProcessing}>
            <p>
              {isProcessing
                ? 'Processing payment...'
                : 'Present card to terminal or tap to pay'}
            </p>
          </article>

          <button
            className="secondary outline"
            onClick={() => setPaymentMethod(null)}
          >
            Change Method
          </button>
        </section>
      )}

      <footer className="payment-footer">
        <button
          onClick={handleCompletePayment}
          disabled={!canComplete || isProcessing}
          aria-busy={isProcessing}
        >
          {isProcessing ? 'Processing...' : `Complete Payment ${formatCurrency(totalWithTip)}`}
        </button>
      </footer>
    </main>
  )
}
