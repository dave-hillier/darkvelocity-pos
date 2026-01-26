import { createContext, useContext, useReducer, type ReactNode } from 'react'
import type { Order, MenuItem } from '../types'
import { orderReducer, initialOrderState, type OrderState, type OrderAction } from '../reducers/orderReducer'

interface OrderContextValue extends OrderState {
  createOrder: (orderType: Order['orderType']) => void
  addItem: (item: MenuItem, quantity?: number) => void
  removeItem: (lineId: string) => void
  changeQuantity: (lineId: string, quantity: number) => void
  applyDiscount: (lineId: string, amount: number, reason?: string) => void
  selectLine: (lineId: string | null) => void
  clearOrder: () => void
  completePayment: (paymentId: string) => void
  setKeypadValue: (value: string) => void
  enterEditMode: () => void
  exitEditMode: () => void
  toggleLine: (lineId: string) => void
  removeSelectedLines: () => void
  clearUnsentItems: () => void
  applyOrderDiscount: (amount: number, reason?: string) => void
  sendOrder: () => void
  dispatch: React.Dispatch<OrderAction>
}

const OrderContext = createContext<OrderContextValue | null>(null)

export function OrderProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(orderReducer, initialOrderState)

  function createOrder(orderType: Order['orderType']) {
    dispatch({ type: 'ORDER_CREATED', payload: { orderType } })
  }

  function addItem(item: MenuItem, quantity?: number) {
    if (!state.order) {
      createOrder('direct_sale')
    }
    // Use keypad value if no quantity specified and keypad has a value
    const qty = quantity ?? (state.keypadValue ? parseInt(state.keypadValue, 10) : 1)
    dispatch({ type: 'ITEM_ADDED', payload: { item, quantity: isNaN(qty) ? 1 : qty } })
    // Clear keypad after adding
    if (state.keypadValue) {
      dispatch({ type: 'KEYPAD_VALUE_CHANGED', payload: { value: '' } })
    }
  }

  function removeItem(lineId: string) {
    dispatch({ type: 'ITEM_REMOVED', payload: { lineId } })
  }

  function changeQuantity(lineId: string, quantity: number) {
    dispatch({ type: 'QUANTITY_CHANGED', payload: { lineId, quantity } })
  }

  function applyDiscount(lineId: string, amount: number, reason?: string) {
    dispatch({ type: 'DISCOUNT_APPLIED', payload: { lineId, amount, reason } })
  }

  function selectLine(lineId: string | null) {
    dispatch({ type: 'LINE_SELECTED', payload: { lineId } })
  }

  function clearOrder() {
    dispatch({ type: 'ORDER_CLEARED' })
  }

  function completePayment(paymentId: string) {
    dispatch({ type: 'ORDER_PAID', payload: { paymentId } })
  }

  function setKeypadValue(value: string) {
    dispatch({ type: 'KEYPAD_VALUE_CHANGED', payload: { value } })
  }

  function enterEditMode() {
    dispatch({ type: 'EDIT_MODE_ENTERED' })
  }

  function exitEditMode() {
    dispatch({ type: 'EDIT_MODE_EXITED' })
  }

  function toggleLine(lineId: string) {
    dispatch({ type: 'LINE_TOGGLED', payload: { lineId } })
  }

  function removeSelectedLines() {
    if (state.selectedLineIds.length > 0) {
      dispatch({ type: 'LINES_REMOVED', payload: { lineIds: state.selectedLineIds } })
    }
  }

  function clearUnsentItems() {
    dispatch({ type: 'UNSENT_ITEMS_CLEARED' })
  }

  function applyOrderDiscount(amount: number, reason?: string) {
    dispatch({ type: 'ORDER_DISCOUNT_APPLIED', payload: { amount, reason } })
  }

  function sendOrder() {
    dispatch({ type: 'ORDER_SENT', payload: { sentAt: new Date().toISOString() } })
  }

  return (
    <OrderContext.Provider
      value={{
        ...state,
        createOrder,
        addItem,
        removeItem,
        changeQuantity,
        applyDiscount,
        selectLine,
        clearOrder,
        completePayment,
        setKeypadValue,
        enterEditMode,
        exitEditMode,
        toggleLine,
        removeSelectedLines,
        clearUnsentItems,
        applyOrderDiscount,
        sendOrder,
        dispatch,
      }}
    >
      {children}
    </OrderContext.Provider>
  )
}

export function useOrder() {
  const context = useContext(OrderContext)
  if (!context) {
    throw new Error('useOrder must be used within an OrderProvider')
  }
  return context
}
