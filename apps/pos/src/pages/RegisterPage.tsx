import { useEffect } from 'react'
import OrderPanel from '../components/OrderPanel'
import MenuPanel from '../components/MenuPanel'
import KeypadPanel from '../components/KeypadPanel'
import EditOrderPanel from '../components/EditOrderPanel'
import { useOrder } from '../contexts/OrderContext'
import { useRegisterDisplay } from '../contexts/DisplayContext'

function DisplayIndicator() {
  const { isDisplayConnected } = useRegisterDisplay()

  return (
    <span className={`display-indicator ${isDisplayConnected ? 'connected' : ''}`}>
      <span className="indicator-dot" />
      {isDisplayConnected ? 'Display' : 'No Display'}
    </span>
  )
}

export default function RegisterPage() {
  const { editMode, order } = useOrder()
  const { sendOrderUpdate, sendOrderCleared } = useRegisterDisplay()

  // Send order updates to customer display whenever order changes
  useEffect(() => {
    if (order && order.lines.length > 0) {
      sendOrderUpdate(order)
    } else if (!order || order.lines.length === 0) {
      sendOrderCleared()
    }
  }, [order, sendOrderUpdate, sendOrderCleared])

  if (editMode) {
    return (
      <div className="pos-layout edit-mode">
        <EditOrderPanel />
      </div>
    )
  }

  return (
    <div className="pos-layout">
      <OrderPanel displayIndicator={<DisplayIndicator />} />
      <MenuPanel />
      <KeypadPanel />
    </div>
  )
}
