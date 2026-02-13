import { Routes, Route, Navigate } from 'react-router-dom'
import { AuthProvider } from './contexts/AuthContext'
import { DeviceAuthProvider, useDeviceAuth } from './contexts/DeviceAuthContext'
import { OrderProvider } from './contexts/OrderContext'
import { MenuProvider } from './contexts/MenuContext'
import DeviceSetupPage from './pages/DeviceSetupPage'
import LoginPage from './pages/LoginPage'
import RegisterPage from './pages/RegisterPage'
import PaymentPage from './pages/PaymentPage'
import TablesPage from './pages/TablesPage'
import { useAuth } from './contexts/AuthContext'

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

function UserProtectedRoute({ children }: { children: React.ReactNode }) {
  const { user, isLoading } = useAuth()

  if (isLoading) {
    return (
      <main className="container">
        <article aria-busy="true">Loading...</article>
      </main>
    )
  }

  if (!user) {
    return <Navigate to="/login" replace />
  }

  return <>{children}</>
}

function AppRoutes() {
  return (
    <Routes>
      <Route path="/setup" element={<DeviceSetupPage />} />
      <Route
        path="/login"
        element={
          <DeviceProtectedRoute>
            <LoginPage />
          </DeviceProtectedRoute>
        }
      />
      <Route
        path="/register"
        element={
          <DeviceProtectedRoute>
            <UserProtectedRoute>
              <MenuProvider>
                <OrderProvider>
                  <RegisterPage />
                </OrderProvider>
              </MenuProvider>
            </UserProtectedRoute>
          </DeviceProtectedRoute>
        }
      />
      <Route
        path="/payment"
        element={
          <DeviceProtectedRoute>
            <UserProtectedRoute>
              <OrderProvider>
                <PaymentPage />
              </OrderProvider>
            </UserProtectedRoute>
          </DeviceProtectedRoute>
        }
      />
      <Route
        path="/tables"
        element={
          <DeviceProtectedRoute>
            <UserProtectedRoute>
              <TablesPage />
            </UserProtectedRoute>
          </DeviceProtectedRoute>
        }
      />
      <Route path="/" element={<Navigate to="/register" replace />} />
    </Routes>
  )
}

export default function App() {
  return (
    <DeviceAuthProvider>
      <AuthProvider>
        <AppRoutes />
      </AuthProvider>
    </DeviceAuthProvider>
  )
}
