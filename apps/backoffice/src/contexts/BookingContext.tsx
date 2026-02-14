import { createContext, useContext, useReducer, type ReactNode } from 'react'
import { bookingReducer, initialBookingState, type BookingState, type BookingAction } from '../reducers/bookingReducer'
import * as bookingApi from '../api/bookings'
import * as tableApi from '../api/tables'
import * as floorPlanApi from '../api/floorPlans'
import type { Booking } from '../api/bookings'
import type { Table, TablePosition } from '../api/tables'
import type { FloorPlan } from '../api/floorPlans'

interface BookingContextValue extends BookingState {
  loadBookings: (bookings: Booking[]) => void
  loadTables: (tables: Table[]) => void
  loadFloorPlans: (floorPlans: FloorPlan[]) => void
  selectBooking: (booking: Booking) => void
  deselectBooking: () => void
  requestBooking: (data: Parameters<typeof bookingApi.requestBooking>[0]) => Promise<void>
  confirmBooking: (bookingId: string) => Promise<void>
  cancelBooking: (bookingId: string, reason: string, cancelledBy: string) => Promise<void>
  checkinBooking: (bookingId: string, checkedInBy: string) => Promise<void>
  seatBooking: (bookingId: string, tableId: string, tableNumber: string, seatedBy: string) => Promise<void>
  completeBooking: (bookingId: string) => Promise<void>
  noShowBooking: (bookingId: string, markedBy?: string) => Promise<void>
  fetchBookingsForDate: (date?: string) => Promise<void>
  fetchTablesForSite: () => Promise<void>
  fetchFloorPlan: (floorPlanId: string) => Promise<FloorPlan | null>
  createFloorPlan: (data: Parameters<typeof floorPlanApi.createFloorPlan>[0]) => Promise<string | null>
  createTable: (data: Parameters<typeof tableApi.createTable>[0]) => Promise<void>
  createTableOnPlan: (floorPlanId: string, data: Parameters<typeof tableApi.createTable>[0]) => Promise<Table | null>
  updateTablePosition: (tableId: string, position: TablePosition) => Promise<void>
  removeTableFromPlan: (floorPlanId: string, tableId: string) => Promise<void>
  seatGuests: (tableId: string, data: Parameters<typeof tableApi.seatGuests>[1]) => Promise<void>
  clearTable: (tableId: string) => Promise<void>
  dispatch: React.Dispatch<BookingAction>
}

const BookingContext = createContext<BookingContextValue | null>(null)

export function BookingProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(bookingReducer, initialBookingState)

  function loadBookings(bookings: Booking[]) {
    dispatch({ type: 'BOOKINGS_LOADED', payload: { bookings } })
  }

  function loadTables(tables: Table[]) {
    dispatch({ type: 'TABLES_LOADED', payload: { tables } })
  }

  function loadFloorPlans(floorPlans: FloorPlan[]) {
    dispatch({ type: 'FLOOR_PLANS_LOADED', payload: { floorPlans } })
  }

  function selectBooking(booking: Booking) {
    dispatch({ type: 'BOOKING_SELECTED', payload: { booking } })
  }

  function deselectBooking() {
    dispatch({ type: 'BOOKING_DESELECTED' })
  }

  async function requestBooking(data: Parameters<typeof bookingApi.requestBooking>[0]) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const result = await bookingApi.requestBooking(data)
      const booking = await bookingApi.getBooking(result.id)
      dispatch({ type: 'BOOKING_REQUESTED', payload: { booking } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function confirmBooking(bookingId: string) {
    try {
      await bookingApi.confirmBooking(bookingId)
      dispatch({ type: 'BOOKING_CONFIRMED', payload: { bookingId } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function cancelBooking(bookingId: string, reason: string, cancelledBy: string) {
    try {
      await bookingApi.cancelBooking(bookingId, { reason, cancelledBy })
      dispatch({ type: 'BOOKING_CANCELLED', payload: { bookingId, reason } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function checkinBooking(bookingId: string, checkedInBy: string) {
    try {
      const result = await bookingApi.checkinBooking(bookingId, { checkedInBy })
      dispatch({ type: 'BOOKING_CHECKED_IN', payload: { bookingId, arrivedAt: result.arrivedAt } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function seatBooking(bookingId: string, tableId: string, tableNumber: string, seatedBy: string) {
    try {
      await bookingApi.seatBooking(bookingId, { tableId, tableNumber, seatedBy })
      dispatch({ type: 'BOOKING_SEATED', payload: { bookingId, tableId } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function completeBooking(bookingId: string) {
    try {
      await bookingApi.completeBooking(bookingId)
      dispatch({ type: 'BOOKING_COMPLETED', payload: { bookingId } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function noShowBooking(bookingId: string, markedBy?: string) {
    try {
      await bookingApi.noShowBooking(bookingId, { markedBy })
      dispatch({ type: 'BOOKING_NO_SHOW', payload: { bookingId } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function fetchBookingsForDate(date?: string) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const result = await bookingApi.fetchBookings(date)
      const bookings = (result._embedded?.items ?? []).map((ref: bookingApi.BookingReference) => ({
        id: ref.bookingId,
        guest: { name: ref.guestName },
        requestedTime: ref.time,
        partySize: ref.partySize,
        status: ref.status,
        confirmationCode: ref.confirmationCode,
        source: 'Direct',
        _links: {},
      } as bookingApi.Booking))
      dispatch({ type: 'BOOKINGS_LOADED', payload: { bookings } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function fetchTablesForSite() {
    try {
      const result = await tableApi.fetchTables()
      const tables = result._embedded?.items ?? []
      dispatch({ type: 'TABLES_LOADED', payload: { tables } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function fetchFloorPlan(floorPlanId: string): Promise<FloorPlan | null> {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const floorPlan = await floorPlanApi.getFloorPlan(floorPlanId)
      dispatch({ type: 'FLOOR_PLAN_LOADED', payload: { floorPlan } })
      return floorPlan
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
      return null
    }
  }

  async function createFloorPlanFn(data: Parameters<typeof floorPlanApi.createFloorPlan>[0]): Promise<string | null> {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const result = await floorPlanApi.createFloorPlan(data)
      const floorPlan = await floorPlanApi.getFloorPlan(result.id)
      dispatch({ type: 'FLOOR_PLAN_LOADED', payload: { floorPlan } })
      return result.id
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
      return null
    }
  }

  async function createTable(data: Parameters<typeof tableApi.createTable>[0]) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const result = await tableApi.createTable(data)
      const table = await tableApi.getTable(result.id)
      dispatch({ type: 'TABLES_LOADED', payload: { tables: [...state.tables, table] } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function createTableOnPlan(floorPlanId: string, data: Parameters<typeof tableApi.createTable>[0]): Promise<Table | null> {
    try {
      const result = await tableApi.createTable({ ...data, floorPlanId })
      await floorPlanApi.addTable(floorPlanId, result.id)
      const table = await tableApi.getTable(result.id)
      dispatch({ type: 'TABLES_LOADED', payload: { tables: [...state.tables, table] } })
      return table
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
      return null
    }
  }

  async function updateTablePosition(tableId: string, position: TablePosition) {
    try {
      const table = await tableApi.updateTable(tableId, { position })
      dispatch({ type: 'TABLE_UPDATED', payload: { table } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function removeTableFromPlan(floorPlanId: string, tableId: string) {
    try {
      await floorPlanApi.removeTable(floorPlanId, tableId)
      await tableApi.deleteTable(tableId)
      dispatch({ type: 'TABLES_LOADED', payload: { tables: state.tables.filter(t => t.id !== tableId) } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function seatGuests(tableId: string, data: Parameters<typeof tableApi.seatGuests>[1]) {
    try {
      const result = await tableApi.seatGuests(tableId, data)
      dispatch({ type: 'TABLE_STATUS_CHANGED', payload: { tableId, status: result.status } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function clearTable(tableId: string) {
    try {
      const result = await tableApi.clearTable(tableId)
      dispatch({ type: 'TABLE_STATUS_CHANGED', payload: { tableId, status: result.status } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  return (
    <BookingContext.Provider
      value={{
        ...state,
        loadBookings,
        loadTables,
        loadFloorPlans,
        selectBooking,
        deselectBooking,
        requestBooking,
        confirmBooking,
        cancelBooking,
        checkinBooking,
        seatBooking,
        completeBooking,
        noShowBooking,
        fetchBookingsForDate,
        fetchTablesForSite,
        fetchFloorPlan,
        createFloorPlan: createFloorPlanFn,
        createTable,
        createTableOnPlan,
        updateTablePosition,
        removeTableFromPlan,
        seatGuests,
        clearTable,
        dispatch,
      }}
    >
      {children}
    </BookingContext.Provider>
  )
}

export function useBookings() {
  const context = useContext(BookingContext)
  if (!context) {
    throw new Error('useBookings must be used within a BookingProvider')
  }
  return context
}
