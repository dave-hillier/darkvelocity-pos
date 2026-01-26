import OrderPanel from '../components/OrderPanel'
import MenuPanel from '../components/MenuPanel'
import KeypadPanel from '../components/KeypadPanel'

export default function RegisterPage() {
  return (
    <div className="pos-layout">
      <OrderPanel />
      <MenuPanel />
      <KeypadPanel />
    </div>
  )
}
