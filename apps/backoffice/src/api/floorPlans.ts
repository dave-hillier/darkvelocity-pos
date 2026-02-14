import { apiClient } from './client'
import type { HalResource } from '../types'

export type FloorPlanElementType = 'Wall' | 'Door' | 'Divider'

export interface FloorPlanElement {
  id: string
  type: FloorPlanElementType
  x: number
  y: number
  width: number
  height: number
  rotation: number
  label?: string
}

export interface FloorPlan extends HalResource {
  id: string
  name: string
  isDefault: boolean
  isActive: boolean
  width: number
  height: number
  backgroundImageUrl?: string
  tableIds: string[]
  sections: FloorPlanSection[]
  elements: FloorPlanElement[]
  createdAt: string
}

export interface FloorPlanSection {
  id: string
  name: string
  color?: string
  tableIds: string[]
}

export async function createFloorPlan(data: {
  name: string
  isDefault?: boolean
  width?: number
  height?: number
}): Promise<{ id: string; name: string; createdAt: string } & HalResource> {
  const endpoint = apiClient.buildOrgSitePath('/floor-plans')
  return apiClient.post(endpoint, data)
}

export async function getFloorPlan(floorPlanId: string): Promise<FloorPlan> {
  const endpoint = apiClient.buildOrgSitePath(`/floor-plans/${floorPlanId}`)
  return apiClient.get(endpoint)
}

export async function updateFloorPlan(floorPlanId: string, data: {
  name?: string
  width?: number
  height?: number
  backgroundImageUrl?: string
  isActive?: boolean
}): Promise<FloorPlan> {
  const endpoint = apiClient.buildOrgSitePath(`/floor-plans/${floorPlanId}`)
  return apiClient.patch(endpoint, data)
}

export async function addTable(floorPlanId: string, tableId: string): Promise<{ tableId: string; added: boolean } & HalResource> {
  const endpoint = apiClient.buildOrgSitePath(`/floor-plans/${floorPlanId}/tables`)
  return apiClient.post(endpoint, { tableId })
}

export async function removeTable(floorPlanId: string, tableId: string): Promise<void> {
  const endpoint = apiClient.buildOrgSitePath(`/floor-plans/${floorPlanId}/tables/${tableId}`)
  return apiClient.delete(endpoint)
}

export async function addElement(floorPlanId: string, data: {
  type: FloorPlanElementType
  x: number
  y: number
  width: number
  height: number
  rotation?: number
  label?: string
}): Promise<FloorPlanElement & HalResource> {
  const endpoint = apiClient.buildOrgSitePath(`/floor-plans/${floorPlanId}/elements`)
  return apiClient.post(endpoint, data)
}

export async function updateElement(floorPlanId: string, elementId: string, data: {
  x?: number
  y?: number
  width?: number
  height?: number
  rotation?: number
  label?: string
}): Promise<FloorPlanElement & HalResource> {
  const endpoint = apiClient.buildOrgSitePath(`/floor-plans/${floorPlanId}/elements/${elementId}`)
  return apiClient.patch(endpoint, data)
}

export async function removeElement(floorPlanId: string, elementId: string): Promise<void> {
  const endpoint = apiClient.buildOrgSitePath(`/floor-plans/${floorPlanId}/elements/${elementId}`)
  return apiClient.delete(endpoint)
}
