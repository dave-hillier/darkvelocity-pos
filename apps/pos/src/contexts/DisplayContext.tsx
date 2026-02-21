import { createContext, useContext, useState, useEffect, useCallback, useRef, type ReactNode } from 'react'
import type { Order } from '../types'

// Message types exchanged between register and display
export type DisplayMessageType =
  | 'DISPLAY_PING'
  | 'DISPLAY_PONG'
  | 'ORDER_UPDATED'
  | 'ORDER_CLEARED'
  | 'REQUEST_TIP'
  | 'TIP_SELECTED'
  | 'REQUEST_PAYMENT'
  | 'PAYMENT_METHOD_SELECTED'
  | 'PAYMENT_PROCESSING'
  | 'REQUEST_RECEIPT'
  | 'RECEIPT_TYPE_SELECTED'
  | 'PAYMENT_COMPLETE'
  | 'IDLE'

export type PaymentMethodChoice = 'cash' | 'card'
export type ReceiptTypeChoice = 'print' | 'email' | 'none'

export interface DisplayMessage {
  type: DisplayMessageType
  order?: Order | null
  tipAmount?: number
  tipPercent?: number
  paymentMethod?: PaymentMethodChoice
  receiptType?: ReceiptTypeChoice
  totalPaid?: number
  changeAmount?: number
}

// Display screen states (derived from messages)
export type DisplayScreen =
  | 'idle'
  | 'order'
  | 'tip'
  | 'payment'
  | 'processing'
  | 'receipt'
  | 'thankyou'

const CHANNEL_NAME = 'darkvelocity-customer-display'

// --- Register-side context (sends commands to display) ---

interface RegisterDisplayContextValue {
  isDisplayConnected: boolean
  sendOrderUpdate: (order: Order | null) => void
  sendOrderCleared: () => void
  requestTip: (order: Order) => void
  requestPayment: (order: Order) => void
  sendPaymentProcessing: (order: Order) => void
  requestReceipt: (order: Order, totalPaid: number, changeAmount?: number) => void
  sendPaymentComplete: (order: Order, totalPaid: number, changeAmount?: number) => void
  sendIdle: () => void
  onTipSelected: (callback: (tipAmount: number, tipPercent: number) => void) => void
  onPaymentMethodSelected: (callback: (method: PaymentMethodChoice) => void) => void
  onReceiptTypeSelected: (callback: (receiptType: ReceiptTypeChoice) => void) => void
}

const RegisterDisplayContext = createContext<RegisterDisplayContextValue | null>(null)

export function RegisterDisplayProvider({ children }: { children: ReactNode }) {
  const [isDisplayConnected, setIsDisplayConnected] = useState(false)
  const channelRef = useRef<BroadcastChannel | null>(null)
  const tipCallbackRef = useRef<((tipAmount: number, tipPercent: number) => void) | null>(null)
  const paymentCallbackRef = useRef<((method: PaymentMethodChoice) => void) | null>(null)
  const receiptCallbackRef = useRef<((receiptType: ReceiptTypeChoice) => void) | null>(null)
  const pingIntervalRef = useRef<ReturnType<typeof setInterval> | null>(null)
  const pongTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  useEffect(() => {
    const channel = new BroadcastChannel(CHANNEL_NAME)
    channelRef.current = channel

    channel.onmessage = (event: MessageEvent<DisplayMessage>) => {
      const msg = event.data
      switch (msg.type) {
        case 'DISPLAY_PONG':
          setIsDisplayConnected(true)
          if (pongTimeoutRef.current) {
            clearTimeout(pongTimeoutRef.current)
            pongTimeoutRef.current = null
          }
          break
        case 'TIP_SELECTED':
          if (msg.tipAmount !== undefined && msg.tipPercent !== undefined) {
            tipCallbackRef.current?.(msg.tipAmount, msg.tipPercent)
          }
          break
        case 'PAYMENT_METHOD_SELECTED':
          if (msg.paymentMethod) {
            paymentCallbackRef.current?.(msg.paymentMethod)
          }
          break
        case 'RECEIPT_TYPE_SELECTED':
          if (msg.receiptType) {
            receiptCallbackRef.current?.(msg.receiptType)
          }
          break
      }
    }

    // Ping display periodically to check connection
    pingIntervalRef.current = setInterval(() => {
      channel.postMessage({ type: 'DISPLAY_PING' } satisfies DisplayMessage)
      // If no pong within 2s, mark disconnected
      pongTimeoutRef.current = setTimeout(() => {
        setIsDisplayConnected(false)
      }, 2000)
    }, 5000)

    // Initial ping
    channel.postMessage({ type: 'DISPLAY_PING' } satisfies DisplayMessage)

    return () => {
      channel.close()
      if (pingIntervalRef.current) clearInterval(pingIntervalRef.current)
      if (pongTimeoutRef.current) clearTimeout(pongTimeoutRef.current)
    }
  }, [])

  const send = useCallback((msg: DisplayMessage) => {
    channelRef.current?.postMessage(msg)
  }, [])

  const sendOrderUpdate = useCallback((order: Order | null) => {
    send({ type: 'ORDER_UPDATED', order })
  }, [send])

  const sendOrderCleared = useCallback(() => {
    send({ type: 'ORDER_CLEARED' })
  }, [send])

  const requestTip = useCallback((order: Order) => {
    send({ type: 'REQUEST_TIP', order })
  }, [send])

  const requestPayment = useCallback((order: Order) => {
    send({ type: 'REQUEST_PAYMENT', order })
  }, [send])

  const sendPaymentProcessing = useCallback((order: Order) => {
    send({ type: 'PAYMENT_PROCESSING', order })
  }, [send])

  const requestReceipt = useCallback((order: Order, totalPaid: number, changeAmount?: number) => {
    send({ type: 'REQUEST_RECEIPT', order, totalPaid, changeAmount })
  }, [send])

  const sendPaymentComplete = useCallback((order: Order, totalPaid: number, changeAmount?: number) => {
    send({ type: 'PAYMENT_COMPLETE', order, totalPaid, changeAmount })
  }, [send])

  const sendIdle = useCallback(() => {
    send({ type: 'IDLE' })
  }, [send])

  const onTipSelected = useCallback((callback: (tipAmount: number, tipPercent: number) => void) => {
    tipCallbackRef.current = callback
  }, [])

  const onPaymentMethodSelected = useCallback((callback: (method: PaymentMethodChoice) => void) => {
    paymentCallbackRef.current = callback
  }, [])

  const onReceiptTypeSelected = useCallback((callback: (receiptType: ReceiptTypeChoice) => void) => {
    receiptCallbackRef.current = callback
  }, [])

  return (
    <RegisterDisplayContext.Provider
      value={{
        isDisplayConnected,
        sendOrderUpdate,
        sendOrderCleared,
        requestTip,
        requestPayment,
        sendPaymentProcessing,
        requestReceipt,
        sendPaymentComplete,
        sendIdle,
        onTipSelected,
        onPaymentMethodSelected,
        onReceiptTypeSelected,
      }}
    >
      {children}
    </RegisterDisplayContext.Provider>
  )
}

export function useRegisterDisplay() {
  const context = useContext(RegisterDisplayContext)
  if (!context) {
    throw new Error('useRegisterDisplay must be used within a RegisterDisplayProvider')
  }
  return context
}

// --- Display-side context (receives commands, sends responses) ---

interface CustomerDisplayState {
  screen: DisplayScreen
  order: Order | null
  totalPaid: number
  changeAmount: number
}

interface CustomerDisplayContextValue extends CustomerDisplayState {
  selectTip: (tipAmount: number, tipPercent: number) => void
  selectPaymentMethod: (method: PaymentMethodChoice) => void
  selectReceiptType: (receiptType: ReceiptTypeChoice) => void
}

const CustomerDisplayContext = createContext<CustomerDisplayContextValue | null>(null)

export function CustomerDisplayProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<CustomerDisplayState>({
    screen: 'idle',
    order: null,
    totalPaid: 0,
    changeAmount: 0,
  })
  const channelRef = useRef<BroadcastChannel | null>(null)

  useEffect(() => {
    const channel = new BroadcastChannel(CHANNEL_NAME)
    channelRef.current = channel

    channel.onmessage = (event: MessageEvent<DisplayMessage>) => {
      const msg = event.data
      switch (msg.type) {
        case 'DISPLAY_PING':
          channel.postMessage({ type: 'DISPLAY_PONG' } satisfies DisplayMessage)
          break
        case 'ORDER_UPDATED':
          setState(prev => ({
            ...prev,
            screen: 'order',
            order: msg.order ?? null,
          }))
          break
        case 'ORDER_CLEARED':
          setState(prev => ({
            ...prev,
            screen: 'idle',
            order: null,
          }))
          break
        case 'REQUEST_TIP':
          setState(prev => ({
            ...prev,
            screen: 'tip',
            order: msg.order ?? prev.order,
          }))
          break
        case 'REQUEST_PAYMENT':
          setState(prev => ({
            ...prev,
            screen: 'payment',
            order: msg.order ?? prev.order,
          }))
          break
        case 'PAYMENT_PROCESSING':
          setState(prev => ({
            ...prev,
            screen: 'processing',
            order: msg.order ?? prev.order,
          }))
          break
        case 'REQUEST_RECEIPT':
          setState(prev => ({
            ...prev,
            screen: 'receipt',
            order: msg.order ?? prev.order,
            totalPaid: msg.totalPaid ?? prev.totalPaid,
            changeAmount: msg.changeAmount ?? 0,
          }))
          break
        case 'PAYMENT_COMPLETE':
          setState(prev => ({
            ...prev,
            screen: 'thankyou',
            order: msg.order ?? prev.order,
            totalPaid: msg.totalPaid ?? prev.totalPaid,
            changeAmount: msg.changeAmount ?? 0,
          }))
          break
        case 'IDLE':
          setState({
            screen: 'idle',
            order: null,
            totalPaid: 0,
            changeAmount: 0,
          })
          break
      }
    }

    return () => {
      channel.close()
    }
  }, [])

  const send = useCallback((msg: DisplayMessage) => {
    channelRef.current?.postMessage(msg)
  }, [])

  const selectTip = useCallback((tipAmount: number, tipPercent: number) => {
    send({ type: 'TIP_SELECTED', tipAmount, tipPercent })
  }, [send])

  const selectPaymentMethod = useCallback((method: PaymentMethodChoice) => {
    send({ type: 'PAYMENT_METHOD_SELECTED', paymentMethod: method })
  }, [send])

  const selectReceiptType = useCallback((receiptType: ReceiptTypeChoice) => {
    send({ type: 'RECEIPT_TYPE_SELECTED', receiptType })
  }, [send])

  return (
    <CustomerDisplayContext.Provider
      value={{
        ...state,
        selectTip,
        selectPaymentMethod,
        selectReceiptType,
      }}
    >
      {children}
    </CustomerDisplayContext.Provider>
  )
}

export function useCustomerDisplay() {
  const context = useContext(CustomerDisplayContext)
  if (!context) {
    throw new Error('useCustomerDisplay must be used within a CustomerDisplayProvider')
  }
  return context
}
