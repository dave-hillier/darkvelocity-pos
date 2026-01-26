// Auth types
export interface User {
  id: string
  username: string
  firstName: string
  lastName: string
  email?: string
  userGroupName: string
  homeLocationId: string
  isActive: boolean
}

export interface LoginResponse {
  accessToken: string
  refreshToken: string
  expiresAt: string
  user: User
}

// Menu types
export interface MenuItem {
  id: string
  name: string
  price: number
  categoryId: string
  accountingGroupId: string
}

export interface MenuCategory {
  id: string
  name: string
  displayOrder: number
  color?: string
}

// Order types
export interface OrderLine {
  id: string
  menuItemId: string
  itemName: string
  quantity: number
  unitPrice: number
  discountAmount: number
  lineTotal: number
}

export interface Order {
  id: string
  orderNumber: string
  orderType: 'direct_sale' | 'table_service'
  status: 'open' | 'completed' | 'voided'
  lines: OrderLine[]
  subtotal: number
  taxTotal: number
  discountTotal: number
  grandTotal: number
}

// HAL types
export interface HalLink {
  href: string
  title?: string
}

export interface HalResource {
  _links: Record<string, HalLink>
}

export interface HalCollection<T> extends HalResource {
  _embedded: {
    items: T[]
  }
  count: number
  total?: number
}
