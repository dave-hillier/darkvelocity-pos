import { Routes, Route, Navigate } from 'react-router-dom'
import Layout from './components/Layout'
import LoginPage from './pages/LoginPage'
import DashboardPage from './pages/DashboardPage'
import MenuItemsPage from './pages/MenuItemsPage'
import IngredientsPage from './pages/IngredientsPage'
import SuppliersPage from './pages/SuppliersPage'
import ReportsPage from './pages/ReportsPage'

export default function App() {
  // For now, skip auth and show logged in state
  const isAuthenticated = true

  if (!isAuthenticated) {
    return (
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    )
  }

  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<Navigate to="/dashboard" replace />} />
        <Route path="dashboard" element={<DashboardPage />} />
        <Route path="menu/items" element={<MenuItemsPage />} />
        <Route path="inventory/ingredients" element={<IngredientsPage />} />
        <Route path="procurement/suppliers" element={<SuppliersPage />} />
        <Route path="reports" element={<ReportsPage />} />
      </Route>
      <Route path="login" element={<LoginPage />} />
    </Routes>
  )
}
