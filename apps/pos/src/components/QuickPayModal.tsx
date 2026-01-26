import { useNavigate } from 'react-router-dom'
import { useOrder } from '../contexts/OrderContext'

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-GB', {
    style: 'currency',
    currency: 'GBP',
  }).format(amount)
}

interface QuickPayModalProps {
  method: 'cash' | 'card'
  onClose: () => void
}

export default function QuickPayModal({ method, onClose }: QuickPayModalProps) {
  const navigate = useNavigate()
  const { order, completePayment } = useOrder()

  if (!order) {
    onClose()
    return null
  }

  const grandTotal = order.grandTotal
  const orderNumber = order.orderNumber

  function handleConfirm() {
    const paymentId = crypto.randomUUID()
    completePayment(paymentId)
    navigate('/payment/complete', {
      state: {
        paymentMethod: method,
        amountPaid: grandTotal,
        change: 0,
        orderNumber: orderNumber,
      },
    })
  }

  function handleCancel() {
    onClose()
  }

  return (
    <dialog open aria-modal="true" className="quick-pay-modal">
      <article>
        <header>
          <h3>{method === 'cash' ? 'Cash Payment' : 'Card Payment'}</h3>
        </header>

        <p>Complete payment of <strong>{formatCurrency(order.grandTotal)}</strong>?</p>

        <footer>
          <button type="button" className="secondary" onClick={handleCancel}>
            Cancel
          </button>
          <button type="button" onClick={handleConfirm}>
            Confirm {method === 'cash' ? 'Cash' : 'Card'}
          </button>
        </footer>
      </article>
    </dialog>
  )
}
