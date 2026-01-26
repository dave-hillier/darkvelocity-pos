import { apiClient } from './client'
import type { LoginResponse } from '../types'

export async function loginWithPin(pin: string, locationId?: string): Promise<LoginResponse> {
  return apiClient.post<LoginResponse>('/api/auth/login', { pin, locationId })
}

export async function loginWithQr(token: string, locationId?: string): Promise<LoginResponse> {
  return apiClient.post<LoginResponse>('/api/auth/login/qr', { token, locationId })
}

export async function refreshToken(refreshToken: string): Promise<LoginResponse> {
  return apiClient.post<LoginResponse>('/api/auth/refresh', { refreshToken })
}
