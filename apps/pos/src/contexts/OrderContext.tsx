import { createContext, useContext, useReducer, useCallback, type ReactNode } from 'react'
import type { MenuItem, OrderType } from '../types'
import { orderReducer, initialOrderState, type OrderState, type OrderAction } from '../reducers/orderReducer'
import * as orderApi from '../api/orders'
import { useAuth } from './AuthContext'

interface OrderContextValue extends OrderState {
  createOrder: (orderType: OrderType) => void
  createOrderAsync: (orderType: OrderType) => Promise<void>
  addItem: (item: MenuItem, quantity?: number) => void
  addItemAsync: (item: MenuItem, quantity?: number) => Promise<void>
  removeItem: (lineId: string) => void
  removeItemAsync: (lineId: string) => Promise<void>
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
  sendOrderAsync: () => Promise<void>
  voidOrderAsync: (reason: string) => Promise<void>
  // Hold/Fire workflow functions
  holdItemsAsync: (lineIds: string[], reason?: string) => Promise<void>
  releaseItemsAsync: (lineIds: string[]) => Promise<void>
  setItemCourseAsync: (lineIds: string[], courseNumber: number) => Promise<void>
  fireItemsAsync: (lineIds: string[]) => Promise<void>
  fireCourseAsync: (courseNumber: number) => Promise<void>
  fireAllAsync: () => Promise<void>
  holdSelectedItems: (reason?: string) => void
  releaseSelectedItems: () => void
  fireSelectedItems: () => void
  dispatch: React.Dispatch<OrderAction>
}

const OrderContext = createContext<OrderContextValue | null>(null)

export function OrderProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(orderReducer, initialOrderState)
  const { user } = useAuth()

  // Sync: Create order on backend
  const createOrderAsync = useCallback(async (orderType: OrderType) => {
    try {
      const result = await orderApi.createOrder({
        createdBy: user?.id || 'anonymous',
        type: orderType,
      })
      dispatch({
        type: 'ORDER_CREATED',
        payload: {
          orderType,
          orderId: result.id,
          orderNumber: result.orderNumber,
        },
      })
    } catch (error) {
      console.error('Failed to create order on server:', error)
      // Fall back to local order creation
      dispatch({ type: 'ORDER_CREATED', payload: { orderType } })
    }
  }, [user])

  // Local: Create order locally (for offline mode)
  function createOrder(orderType: OrderType) {
    dispatch({ type: 'ORDER_CREATED', payload: { orderType } })
  }

  // Sync: Add item and sync with backend
  const addItemAsync = useCallback(async (item: MenuItem, quantity?: number) => {
    if (!state.order) {
      await createOrderAsync('DirectSale')
    }

    const qty = quantity ?? (state.keypadValue ? parseInt(state.keypadValue, 10) : 1)
    const finalQty = isNaN(qty) ? 1 : qty

    // Update local state first for responsiveness
    dispatch({ type: 'ITEM_ADDED', payload: { item, quantity: finalQty } })

    if (state.keypadValue) {
      dispatch({ type: 'KEYPAD_VALUE_CHANGED', payload: { value: '' } })
    }

    // Sync with backend if order exists
    if (state.order?.id) {
      try {
        await orderApi.addOrderLine(state.order.id, {
          menuItemId: item.id,
          name: item.name,
          quantity: finalQty,
          unitPrice: item.price,
        })
      } catch (error) {
        console.error('Failed to add item on server:', error)
        // Local state is already updated, continue working offline
      }
    }
  }, [state.order, state.keypadValue, createOrderAsync])

  // Local: Add item locally
  function addItem(item: MenuItem, quantity?: number) {
    if (!state.order) {
      createOrder('DirectSale')
    }
    const qty = quantity ?? (state.keypadValue ? parseInt(state.keypadValue, 10) : 1)
    dispatch({ type: 'ITEM_ADDED', payload: { item, quantity: isNaN(qty) ? 1 : qty } })
    if (state.keypadValue) {
      dispatch({ type: 'KEYPAD_VALUE_CHANGED', payload: { value: '' } })
    }
  }

  // Sync: Remove item from backend
  const removeItemAsync = useCallback(async (lineId: string) => {
    dispatch({ type: 'ITEM_REMOVED', payload: { lineId } })

    if (state.order?.id) {
      try {
        await orderApi.removeOrderLine(state.order.id, lineId)
      } catch (error) {
        console.error('Failed to remove item on server:', error)
      }
    }
  }, [state.order])

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

  // Local: Send order (mark items as sent locally)
  function sendOrder() {
    dispatch({ type: 'ORDER_SENT', payload: { sentAt: new Date().toISOString() } })
  }

  // Sync: Send order to kitchen via backend
  const sendOrderAsync = useCallback(async () => {
    if (!state.order?.id) {
      // No server order, just update locally
      dispatch({ type: 'ORDER_SENT', payload: { sentAt: new Date().toISOString() } })
      return
    }

    try {
      const result = await orderApi.sendOrder(state.order.id, {
        sentBy: user?.id || 'anonymous',
      })
      dispatch({
        type: 'ORDER_SENT',
        payload: { sentAt: result.sentAt, status: 'Sent' },
      })
    } catch (error) {
      console.error('Failed to send order to server:', error)
      // Fall back to local update
      dispatch({ type: 'ORDER_SENT', payload: { sentAt: new Date().toISOString() } })
    }
  }, [state.order, user])

  // Sync: Void order on backend
  const voidOrderAsync = useCallback(async (reason: string) => {
    if (!state.order?.id) {
      dispatch({ type: 'ORDER_CLEARED' })
      return
    }

    try {
      await orderApi.voidOrder(state.order.id, {
        voidedBy: user?.id || 'anonymous',
        reason,
      })
      dispatch({ type: 'STATUS_CHANGED', payload: { status: 'Voided' } })
    } catch (error) {
      console.error('Failed to void order on server:', error)
      dispatch({ type: 'STATUS_CHANGED', payload: { status: 'Voided' } })
    }
  }, [state.order, user])

  // Hold/Fire workflow functions

  const holdItemsAsync = useCallback(async (lineIds: string[], reason?: string) => {
    const userId = user?.id || 'anonymous'
    const heldAt = new Date().toISOString()

    // Update local state first
    dispatch({ type: 'ITEMS_HELD', payload: { lineIds, heldAt, heldBy: userId, reason } })

    // Sync with backend
    if (state.order?.id) {
      try {
        await orderApi.holdItems(state.order.id, {
          lineIds,
          heldBy: userId,
          reason,
        })
      } catch (error) {
        console.error('Failed to hold items on server:', error)
      }
    }
  }, [state.order, user])

  const releaseItemsAsync = useCallback(async (lineIds: string[]) => {
    const userId = user?.id || 'anonymous'

    // Update local state first
    dispatch({ type: 'ITEMS_RELEASED', payload: { lineIds } })

    // Sync with backend
    if (state.order?.id) {
      try {
        await orderApi.releaseItems(state.order.id, {
          lineIds,
          releasedBy: userId,
        })
      } catch (error) {
        console.error('Failed to release items on server:', error)
      }
    }
  }, [state.order, user])

  const setItemCourseAsync = useCallback(async (lineIds: string[], courseNumber: number) => {
    const userId = user?.id || 'anonymous'

    // Update local state first
    dispatch({ type: 'ITEMS_COURSE_SET', payload: { lineIds, courseNumber } })

    // Sync with backend
    if (state.order?.id) {
      try {
        await orderApi.setItemCourse(state.order.id, {
          lineIds,
          courseNumber,
          setBy: userId,
        })
      } catch (error) {
        console.error('Failed to set item course on server:', error)
      }
    }
  }, [state.order, user])

  const fireItemsAsync = useCallback(async (lineIds: string[]) => {
    const userId = user?.id || 'anonymous'
    const firedAt = new Date().toISOString()

    // Update local state first
    dispatch({ type: 'ITEMS_FIRED', payload: { lineIds, firedAt, firedBy: userId } })

    // Sync with backend
    if (state.order?.id) {
      try {
        await orderApi.fireItems(state.order.id, {
          lineIds,
          firedBy: userId,
        })
      } catch (error) {
        console.error('Failed to fire items on server:', error)
      }
    }
  }, [state.order, user])

  const fireCourseAsync = useCallback(async (courseNumber: number) => {
    if (!state.order) return

    const userId = user?.id || 'anonymous'
    const firedAt = new Date().toISOString()

    // Find lines in the specified course
    const courseLineIds = state.order.lines
      .filter(line => (line.courseNumber ?? 1) === courseNumber && !line.sentAt)
      .map(line => line.id)

    if (courseLineIds.length === 0) return

    // Update local state first
    dispatch({ type: 'COURSE_FIRED', payload: { courseNumber, firedLineIds: courseLineIds, firedAt, firedBy: userId } })

    // Sync with backend
    if (state.order?.id) {
      try {
        await orderApi.fireCourse(state.order.id, {
          courseNumber,
          firedBy: userId,
        })
      } catch (error) {
        console.error('Failed to fire course on server:', error)
      }
    }
  }, [state.order, user])

  const fireAllAsync = useCallback(async () => {
    if (!state.order) return

    const userId = user?.id || 'anonymous'
    const firedAt = new Date().toISOString()

    // Find all pending lines
    const pendingLineIds = state.order.lines
      .filter(line => !line.sentAt)
      .map(line => line.id)

    if (pendingLineIds.length === 0) return

    // Update local state first
    dispatch({ type: 'ALL_ITEMS_FIRED', payload: { firedLineIds: pendingLineIds, firedAt, firedBy: userId } })

    // Sync with backend
    if (state.order?.id) {
      try {
        await orderApi.fireAll(state.order.id, {
          firedBy: userId,
        })
      } catch (error) {
        console.error('Failed to fire all items on server:', error)
      }
    }
  }, [state.order, user])

  // Local hold/fire operations for selected items
  function holdSelectedItems(reason?: string) {
    if (state.selectedLineIds.length > 0) {
      const userId = user?.id || 'anonymous'
      const heldAt = new Date().toISOString()
      dispatch({ type: 'ITEMS_HELD', payload: { lineIds: state.selectedLineIds, heldAt, heldBy: userId, reason } })
    }
  }

  function releaseSelectedItems() {
    if (state.selectedLineIds.length > 0) {
      dispatch({ type: 'ITEMS_RELEASED', payload: { lineIds: state.selectedLineIds } })
    }
  }

  function fireSelectedItems() {
    if (state.selectedLineIds.length > 0) {
      const userId = user?.id || 'anonymous'
      const firedAt = new Date().toISOString()
      dispatch({ type: 'ITEMS_FIRED', payload: { lineIds: state.selectedLineIds, firedAt, firedBy: userId } })
    }
  }

  return (
    <OrderContext.Provider
      value={{
        ...state,
        createOrder,
        createOrderAsync,
        addItem,
        addItemAsync,
        removeItem,
        removeItemAsync,
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
        sendOrderAsync,
        voidOrderAsync,
        holdItemsAsync,
        releaseItemsAsync,
        setItemCourseAsync,
        fireItemsAsync,
        fireCourseAsync,
        fireAllAsync,
        holdSelectedItems,
        releaseSelectedItems,
        fireSelectedItems,
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
