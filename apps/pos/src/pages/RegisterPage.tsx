import OrderPanel from '../components/OrderPanel'
import MenuPanel from '../components/MenuPanel'
import KeypadPanel from '../components/KeypadPanel'
import EditOrderPanel from '../components/EditOrderPanel'
import { useOrder } from '../contexts/OrderContext'

export default function RegisterPage() {
  const { editMode } = useOrder()

  if (editMode) {
    return (
      <div className="pos-layout edit-mode">
        <EditOrderPanel />
      </div>
    )
  }

  return (
    <div className="pos-layout">
      <OrderPanel />
      <MenuPanel />
      <KeypadPanel />
    </div>
  )
}
