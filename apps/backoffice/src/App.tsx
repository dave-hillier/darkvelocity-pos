import { Routes, Route, Navigate } from 'react-router-dom'
import Layout from './components/Layout'
import { AuthProvider, useAuth } from './contexts/AuthContext'
import LoginPage from './pages/LoginPage'
import DashboardPage from './pages/DashboardPage'
import MenuItemsPage from './pages/MenuItemsPage'
import CategoriesPage from './pages/CategoriesPage'
import RecipesPage from './pages/RecipesPage'
import IngredientsPage from './pages/IngredientsPage'
import StockPage from './pages/StockPage'
import SuppliersPage from './pages/SuppliersPage'
import PurchaseOrdersPage from './pages/PurchaseOrdersPage'
import DeliveriesPage from './pages/DeliveriesPage'
import ReportsPage from './pages/ReportsPage'
import MarginAnalysisPage from './pages/MarginAnalysisPage'
import DeviceAuthorizePage from './pages/DeviceAuthorizePage'

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, isLoading } = useAuth()

  if (isLoading) {
    return (
      <main className="container">
        <article aria-busy="true">Loading...</article>
      </main>
    )
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />
  }

  return <>{children}</>
}

function AppRoutes() {
  const { isAuthenticated, isLoading } = useAuth()

  if (isLoading) {
    return (
      <main className="container">
        <article aria-busy="true">Loading...</article>
      </main>
    )
  }

  return (
    <Routes>
      {/* Public routes */}
      <Route path="login" element={<LoginPage />} />
      <Route path="device" element={<DeviceAuthorizePage />} />

      {/* Protected routes */}
      <Route
        element={
          <ProtectedRoute>
            <Layout />
          </ProtectedRoute>
        }
      >
        <Route index element={<Navigate to="/dashboard" replace />} />
        <Route path="dashboard" element={<DashboardPage />} />
        <Route path="menu/items" element={<MenuItemsPage />} />
        <Route path="menu/categories" element={<CategoriesPage />} />
        <Route path="menu/recipes" element={<RecipesPage />} />
        <Route path="inventory/ingredients" element={<IngredientsPage />} />
        <Route path="inventory/stock" element={<StockPage />} />
        <Route path="procurement/suppliers" element={<SuppliersPage />} />
        <Route path="procurement/purchase-orders" element={<PurchaseOrdersPage />} />
        <Route path="procurement/deliveries" element={<DeliveriesPage />} />
        <Route path="reports" element={<ReportsPage />} />
        <Route path="reports/margins" element={<MarginAnalysisPage />} />
        <Route path="devices/authorize" element={<DeviceAuthorizePage />} />
      </Route>

      {/* Catch-all redirect */}
      <Route path="*" element={<Navigate to={isAuthenticated ? '/dashboard' : '/login'} replace />} />
    </Routes>
  )
}

export default function App() {
  return (
    <AuthProvider>
      <AppRoutes />
    </AuthProvider>
  )
}
