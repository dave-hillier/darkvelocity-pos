import { Routes, Route, Navigate } from 'react-router-dom'
import { DeviceAuthProvider, useDeviceAuth } from './contexts/DeviceAuthContext'
import { StationProvider, useStation } from './contexts/StationContext'
import DeviceSetupPage from './pages/DeviceSetupPage'
import StationSelectPage from './pages/StationSelectPage'
import KitchenDisplayPage from './pages/KitchenDisplayPage'

function DeviceProtectedRoute({ children }: { children: React.ReactNode }) {
  const { isDeviceAuthenticated, isLoading } = useDeviceAuth()

  if (isLoading) {
    return (
      <main className="container">
        <article aria-busy="true">Loading...</article>
      </main>
    )
  }

  if (!isDeviceAuthenticated) {
    return <Navigate to="/setup" replace />
  }

  return <>{children}</>
}

function StationProtectedRoute({ children }: { children: React.ReactNode }) {
  const { selectedStation, isLoading } = useStation()

  if (isLoading) {
    return (
      <main className="container">
        <article aria-busy="true">Loading...</article>
      </main>
    )
  }

  if (!selectedStation) {
    return <Navigate to="/station" replace />
  }

  return <>{children}</>
}

function AppRoutes() {
  return (
    <Routes>
      <Route path="/setup" element={<DeviceSetupPage />} />
      <Route
        path="/station"
        element={
          <DeviceProtectedRoute>
            <StationSelectPage />
          </DeviceProtectedRoute>
        }
      />
      <Route
        path="/display"
        element={
          <DeviceProtectedRoute>
            <StationProtectedRoute>
              <KitchenDisplayPage />
            </StationProtectedRoute>
          </DeviceProtectedRoute>
        }
      />
      <Route path="/" element={<Navigate to="/display" replace />} />
    </Routes>
  )
}

export default function App() {
  return (
    <DeviceAuthProvider>
      <StationProvider>
        <AppRoutes />
      </StationProvider>
    </DeviceAuthProvider>
  )
}
