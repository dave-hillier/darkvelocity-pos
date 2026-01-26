import type { Order, OrderLine, MenuItem } from '../types'

export type OrderAction =
  | { type: 'ORDER_CREATED'; payload: { orderType: Order['orderType'] } }
  | { type: 'ITEM_ADDED'; payload: { item: MenuItem; quantity: number } }
  | { type: 'ITEM_REMOVED'; payload: { lineId: string } }
  | { type: 'QUANTITY_CHANGED'; payload: { lineId: string; quantity: number } }
  | { type: 'DISCOUNT_APPLIED'; payload: { lineId: string; amount: number; reason?: string } }
  | { type: 'ORDER_CLEARED' }
  | { type: 'ORDER_PAID'; payload: { paymentId: string } }
  | { type: 'LINE_SELECTED'; payload: { lineId: string | null } }

export interface OrderState {
  order: Order | null
  selectedLineId: string | null
}

export const initialOrderState: OrderState = {
  order: null,
  selectedLineId: null,
}

function generateId(): string {
  return crypto.randomUUID()
}

function generateOrderNumber(): string {
  return `ORD-${Date.now().toString(36).toUpperCase()}`
}

function calculateTotals(lines: OrderLine[]): Pick<Order, 'subtotal' | 'taxTotal' | 'discountTotal' | 'grandTotal'> {
  const subtotal = lines.reduce((sum, line) => sum + line.lineTotal, 0)
  const discountTotal = lines.reduce((sum, line) => sum + line.discountAmount, 0)
  const taxRate = 0.20 // 20% VAT - should come from accounting groups
  const taxTotal = (subtotal - discountTotal) * taxRate
  const grandTotal = subtotal - discountTotal + taxTotal

  return {
    subtotal: Math.round(subtotal * 100) / 100,
    taxTotal: Math.round(taxTotal * 100) / 100,
    discountTotal: Math.round(discountTotal * 100) / 100,
    grandTotal: Math.round(grandTotal * 100) / 100,
  }
}

export function orderReducer(state: OrderState, action: OrderAction): OrderState {
  switch (action.type) {
    case 'ORDER_CREATED': {
      return {
        ...state,
        order: {
          id: generateId(),
          orderNumber: generateOrderNumber(),
          orderType: action.payload.orderType,
          status: 'open',
          lines: [],
          subtotal: 0,
          taxTotal: 0,
          discountTotal: 0,
          grandTotal: 0,
        },
        selectedLineId: null,
      }
    }

    case 'ITEM_ADDED': {
      if (!state.order) return state

      const { item, quantity } = action.payload
      const existingLine = state.order.lines.find(
        (line) => line.menuItemId === item.id && line.discountAmount === 0
      )

      let newLines: OrderLine[]

      if (existingLine) {
        // Increase quantity of existing line
        newLines = state.order.lines.map((line) =>
          line.id === existingLine.id
            ? {
                ...line,
                quantity: line.quantity + quantity,
                lineTotal: (line.quantity + quantity) * line.unitPrice,
              }
            : line
        )
      } else {
        // Add new line
        const newLine: OrderLine = {
          id: generateId(),
          menuItemId: item.id,
          itemName: item.name,
          quantity,
          unitPrice: item.price,
          discountAmount: 0,
          lineTotal: quantity * item.price,
        }
        newLines = [...state.order.lines, newLine]
      }

      return {
        ...state,
        order: {
          ...state.order,
          lines: newLines,
          ...calculateTotals(newLines),
        },
      }
    }

    case 'ITEM_REMOVED': {
      if (!state.order) return state

      const newLines = state.order.lines.filter((line) => line.id !== action.payload.lineId)

      return {
        ...state,
        order: {
          ...state.order,
          lines: newLines,
          ...calculateTotals(newLines),
        },
        selectedLineId: state.selectedLineId === action.payload.lineId ? null : state.selectedLineId,
      }
    }

    case 'QUANTITY_CHANGED': {
      if (!state.order) return state

      const { lineId, quantity } = action.payload

      if (quantity <= 0) {
        // Remove line if quantity is 0 or less
        const newLines = state.order.lines.filter((line) => line.id !== lineId)
        return {
          ...state,
          order: {
            ...state.order,
            lines: newLines,
            ...calculateTotals(newLines),
          },
          selectedLineId: state.selectedLineId === lineId ? null : state.selectedLineId,
        }
      }

      const newLines = state.order.lines.map((line) =>
        line.id === lineId
          ? {
              ...line,
              quantity,
              lineTotal: quantity * line.unitPrice - line.discountAmount,
            }
          : line
      )

      return {
        ...state,
        order: {
          ...state.order,
          lines: newLines,
          ...calculateTotals(newLines),
        },
      }
    }

    case 'DISCOUNT_APPLIED': {
      if (!state.order) return state

      const { lineId, amount } = action.payload

      const newLines = state.order.lines.map((line) =>
        line.id === lineId
          ? {
              ...line,
              discountAmount: amount,
              lineTotal: line.quantity * line.unitPrice - amount,
            }
          : line
      )

      return {
        ...state,
        order: {
          ...state.order,
          lines: newLines,
          ...calculateTotals(newLines),
        },
      }
    }

    case 'ORDER_CLEARED': {
      return initialOrderState
    }

    case 'ORDER_PAID': {
      if (!state.order) return state

      return {
        ...state,
        order: {
          ...state.order,
          status: 'completed',
        },
      }
    }

    case 'LINE_SELECTED': {
      return {
        ...state,
        selectedLineId: action.payload.lineId,
      }
    }

    default:
      return state
  }
}
