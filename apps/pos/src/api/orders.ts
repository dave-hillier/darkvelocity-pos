import { apiClient } from './client'
import type {
  Order,
  OrderLine,
  CreateOrderRequest,
  AddLineRequest,
  SendOrderRequest,
  CloseOrderRequest,
  VoidOrderRequest,
  ApplyDiscountRequest,
  HalResource,
  HoldItemsRequest,
  ReleaseItemsRequest,
  SetItemCourseRequest,
  FireItemsRequest,
  FireCourseRequest,
  FireAllRequest,
  FireResult,
  HoldSummary,
  CourseSummary,
} from '../types'

// Response types with HAL links
export interface OrderResponse extends Order, HalResource {}
export interface OrderLineResponse extends OrderLine, HalResource {}

export interface CreateOrderResult {
  id: string
  orderNumber: string
  createdAt: string
}

export interface OrderTotals {
  subtotal: number
  discountTotal: number
  taxTotal: number
  grandTotal: number
}

export async function createOrder(request: CreateOrderRequest): Promise<CreateOrderResult & HalResource> {
  const endpoint = apiClient.buildOrgSitePath('/orders')
  return apiClient.post<CreateOrderResult & HalResource>(endpoint, request)
}

export async function getOrder(orderId: string): Promise<OrderResponse> {
  const endpoint = apiClient.buildOrgSitePath(`/orders/${orderId}`)
  return apiClient.get<OrderResponse>(endpoint)
}

export async function addOrderLine(orderId: string, request: AddLineRequest): Promise<OrderLineResponse> {
  const endpoint = apiClient.buildOrgSitePath(`/orders/${orderId}/lines`)
  return apiClient.post<OrderLineResponse>(endpoint, request)
}

export async function getOrderLines(orderId: string): Promise<OrderLine[]> {
  const endpoint = apiClient.buildOrgSitePath(`/orders/${orderId}/lines`)
  const response = await apiClient.get<{ _embedded?: { items: OrderLine[] } }>(endpoint)
  return response._embedded?.items ?? []
}

export async function removeOrderLine(orderId: string, lineId: string): Promise<void> {
  const endpoint = apiClient.buildOrgSitePath(`/orders/${orderId}/lines/${lineId}`)
  await apiClient.delete(endpoint)
}

export async function sendOrder(orderId: string, request: SendOrderRequest): Promise<{ status: string; sentAt: string }> {
  const endpoint = apiClient.buildOrgSitePath(`/orders/${orderId}/send`)
  return apiClient.post(endpoint, request)
}

export async function closeOrder(orderId: string, request: CloseOrderRequest): Promise<{ message: string }> {
  const endpoint = apiClient.buildOrgSitePath(`/orders/${orderId}/close`)
  return apiClient.post(endpoint, request)
}

export async function voidOrder(orderId: string, request: VoidOrderRequest): Promise<{ message: string }> {
  const endpoint = apiClient.buildOrgSitePath(`/orders/${orderId}/void`)
  return apiClient.post(endpoint, request)
}

export async function applyDiscount(orderId: string, request: ApplyDiscountRequest): Promise<OrderTotals & HalResource> {
  const endpoint = apiClient.buildOrgSitePath(`/orders/${orderId}/discounts`)
  return apiClient.post(endpoint, request)
}

export async function getOrderTotals(orderId: string): Promise<OrderTotals & HalResource> {
  const endpoint = apiClient.buildOrgSitePath(`/orders/${orderId}/totals`)
  return apiClient.get(endpoint)
}

// Hold/Fire workflow API functions

export async function holdItems(orderId: string, request: HoldItemsRequest): Promise<HoldSummary & HalResource> {
  const endpoint = apiClient.buildOrgSitePath(`/orders/${orderId}/hold`)
  return apiClient.post(endpoint, request)
}

export async function releaseItems(orderId: string, request: ReleaseItemsRequest): Promise<HoldSummary & HalResource> {
  const endpoint = apiClient.buildOrgSitePath(`/orders/${orderId}/release`)
  return apiClient.post(endpoint, request)
}

export async function setItemCourse(orderId: string, request: SetItemCourseRequest): Promise<CourseSummary & HalResource> {
  const endpoint = apiClient.buildOrgSitePath(`/orders/${orderId}/set-course`)
  return apiClient.post(endpoint, request)
}

export async function fireItems(orderId: string, request: FireItemsRequest): Promise<FireResult & HalResource> {
  const endpoint = apiClient.buildOrgSitePath(`/orders/${orderId}/fire`)
  return apiClient.post(endpoint, request)
}

export async function fireCourse(orderId: string, request: FireCourseRequest): Promise<FireResult & HalResource> {
  const endpoint = apiClient.buildOrgSitePath(`/orders/${orderId}/fire-course`)
  return apiClient.post(endpoint, request)
}

export async function fireAll(orderId: string, request: FireAllRequest): Promise<FireResult & HalResource> {
  const endpoint = apiClient.buildOrgSitePath(`/orders/${orderId}/fire-all`)
  return apiClient.post(endpoint, request)
}

export async function getHoldSummary(orderId: string): Promise<HoldSummary & HalResource> {
  const endpoint = apiClient.buildOrgSitePath(`/orders/${orderId}/hold-summary`)
  return apiClient.get(endpoint)
}

export async function getHeldItems(orderId: string): Promise<OrderLine[]> {
  const endpoint = apiClient.buildOrgSitePath(`/orders/${orderId}/held-items`)
  const response = await apiClient.get<{ _embedded?: { items: OrderLine[] } }>(endpoint)
  return response._embedded?.items ?? []
}

export async function getCourseSummary(orderId: string): Promise<CourseSummary & HalResource> {
  const endpoint = apiClient.buildOrgSitePath(`/orders/${orderId}/courses`)
  return apiClient.get(endpoint)
}
