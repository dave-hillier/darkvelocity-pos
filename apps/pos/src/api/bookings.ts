import { apiClient } from './client'

export type BookingStatus =
  | 'Requested'
  | 'PendingDeposit'
  | 'Confirmed'
  | 'Arrived'
  | 'Seated'
  | 'Completed'
  | 'NoShow'
  | 'Cancelled'

export interface BookingReference {
  bookingId: string
  confirmationCode: string
  time: string
  partySize: number
  guestName: string
  status: BookingStatus
  tableId?: string
  tableNumber?: string
}

export interface DayView {
  date: string
  totalBookings: number
  totalCovers: number
  confirmedBookings: number
  seatedBookings: number
  noShowCount: number
  slots: {
    startTime: string
    endTime: string
    bookingCount: number
    coverCount: number
    bookings: BookingReference[]
  }[]
}

export async function fetchDayView(date?: string): Promise<DayView> {
  const params = date ? `?date=${date}` : ''
  const endpoint = apiClient.buildOrgSitePath(`/bookings/day-view${params}`)
  return apiClient.get(endpoint)
}
