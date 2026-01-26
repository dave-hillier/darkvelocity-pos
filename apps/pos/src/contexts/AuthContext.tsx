import { createContext, useContext, useReducer, useEffect, type ReactNode } from 'react'
import type { User, LoginResponse } from '../types'
import { apiClient } from '../api/client'
import * as authApi from '../api/auth'

interface AuthState {
  user: User | null
  accessToken: string | null
  refreshToken: string | null
  locationId: string | null
  isLoading: boolean
  error: string | null
}

type AuthAction =
  | { type: 'AUTH_STARTED' }
  | { type: 'AUTH_SUCCEEDED'; payload: LoginResponse }
  | { type: 'AUTH_FAILED'; payload: string }
  | { type: 'LOGGED_OUT' }
  | { type: 'LOCATION_CHANGED'; payload: string }

const initialState: AuthState = {
  user: null,
  accessToken: null,
  refreshToken: null,
  locationId: null,
  isLoading: true,
  error: null,
}

function authReducer(state: AuthState, action: AuthAction): AuthState {
  switch (action.type) {
    case 'AUTH_STARTED':
      return { ...state, isLoading: true, error: null }
    case 'AUTH_SUCCEEDED':
      return {
        ...state,
        user: action.payload.user,
        accessToken: action.payload.accessToken,
        refreshToken: action.payload.refreshToken,
        locationId: action.payload.user.homeLocationId,
        isLoading: false,
        error: null,
      }
    case 'AUTH_FAILED':
      return {
        ...state,
        user: null,
        accessToken: null,
        refreshToken: null,
        isLoading: false,
        error: action.payload,
      }
    case 'LOGGED_OUT':
      return { ...initialState, isLoading: false }
    case 'LOCATION_CHANGED':
      return { ...state, locationId: action.payload }
    default:
      return state
  }
}

interface AuthContextValue extends AuthState {
  loginWithPin: (pin: string) => Promise<void>
  loginWithQr: (token: string) => Promise<void>
  logout: () => void
  setLocation: (locationId: string) => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

const STORAGE_KEY = 'darkvelocity_auth'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(authReducer, initialState)

  // Load stored auth on mount
  useEffect(() => {
    const stored = localStorage.getItem(STORAGE_KEY)
    if (stored) {
      try {
        const { accessToken, refreshToken, user, locationId } = JSON.parse(stored)
        apiClient.setToken(accessToken)
        dispatch({
          type: 'AUTH_SUCCEEDED',
          payload: { accessToken, refreshToken, user, expiresAt: '' },
        })
        if (locationId) {
          dispatch({ type: 'LOCATION_CHANGED', payload: locationId })
        }
      } catch {
        localStorage.removeItem(STORAGE_KEY)
        dispatch({ type: 'LOGGED_OUT' })
      }
    } else {
      dispatch({ type: 'LOGGED_OUT' })
    }
  }, [])

  // Persist auth to storage
  useEffect(() => {
    if (state.accessToken && state.user) {
      localStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({
          accessToken: state.accessToken,
          refreshToken: state.refreshToken,
          user: state.user,
          locationId: state.locationId,
        })
      )
    }
  }, [state.accessToken, state.refreshToken, state.user, state.locationId])

  async function loginWithPin(pin: string) {
    dispatch({ type: 'AUTH_STARTED' })
    try {
      const response = await authApi.loginWithPin(pin, state.locationId ?? undefined)
      apiClient.setToken(response.accessToken)
      dispatch({ type: 'AUTH_SUCCEEDED', payload: response })
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Login failed'
      dispatch({ type: 'AUTH_FAILED', payload: message })
      throw err
    }
  }

  async function loginWithQr(token: string) {
    dispatch({ type: 'AUTH_STARTED' })
    try {
      const response = await authApi.loginWithQr(token, state.locationId ?? undefined)
      apiClient.setToken(response.accessToken)
      dispatch({ type: 'AUTH_SUCCEEDED', payload: response })
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Login failed'
      dispatch({ type: 'AUTH_FAILED', payload: message })
      throw err
    }
  }

  function logout() {
    localStorage.removeItem(STORAGE_KEY)
    apiClient.setToken(null)
    dispatch({ type: 'LOGGED_OUT' })
  }

  function setLocation(locationId: string) {
    dispatch({ type: 'LOCATION_CHANGED', payload: locationId })
  }

  return (
    <AuthContext.Provider
      value={{
        ...state,
        loginWithPin,
        loginWithQr,
        logout,
        setLocation,
      }}
    >
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return context
}
