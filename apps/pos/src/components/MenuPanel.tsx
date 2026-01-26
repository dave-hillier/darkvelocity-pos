import { useState } from 'react'
import { useOrder } from '../contexts/OrderContext'
import type { MenuItem, MenuCategory } from '../types'

// Sample menu data (would come from API)
const sampleCategories: MenuCategory[] = [
  { id: '1', name: 'Food', displayOrder: 1 },
  { id: '2', name: 'Drinks', displayOrder: 2 },
  { id: '3', name: 'Desserts', displayOrder: 3 },
]

const sampleItems: MenuItem[] = [
  { id: '1', name: 'Burger', price: 12.50, categoryId: '1', accountingGroupId: '1' },
  { id: '2', name: 'Fish & Chips', price: 14.00, categoryId: '1', accountingGroupId: '1' },
  { id: '3', name: 'Caesar Salad', price: 9.50, categoryId: '1', accountingGroupId: '1' },
  { id: '4', name: 'Steak', price: 24.00, categoryId: '1', accountingGroupId: '1' },
  { id: '5', name: 'Pasta', price: 11.00, categoryId: '1', accountingGroupId: '1' },
  { id: '6', name: 'Pizza', price: 13.50, categoryId: '1', accountingGroupId: '1' },
  { id: '7', name: 'Cola', price: 3.00, categoryId: '2', accountingGroupId: '2' },
  { id: '8', name: 'Beer', price: 5.50, categoryId: '2', accountingGroupId: '2' },
  { id: '9', name: 'Wine (Glass)', price: 7.00, categoryId: '2', accountingGroupId: '2' },
  { id: '10', name: 'Coffee', price: 3.50, categoryId: '2', accountingGroupId: '2' },
  { id: '11', name: 'Orange Juice', price: 4.00, categoryId: '2', accountingGroupId: '2' },
  { id: '12', name: 'Water', price: 2.00, categoryId: '2', accountingGroupId: '2' },
  { id: '13', name: 'Cheesecake', price: 6.50, categoryId: '3', accountingGroupId: '1' },
  { id: '14', name: 'Ice Cream', price: 5.00, categoryId: '3', accountingGroupId: '1' },
  { id: '15', name: 'Brownie', price: 5.50, categoryId: '3', accountingGroupId: '1' },
]

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-GB', {
    style: 'currency',
    currency: 'GBP',
  }).format(amount)
}

export default function MenuPanel() {
  const [selectedCategory, setSelectedCategory] = useState<string | null>(sampleCategories[0]?.id ?? null)
  const { addItem } = useOrder()

  const filteredItems = selectedCategory
    ? sampleItems.filter((item) => item.categoryId === selectedCategory)
    : sampleItems

  return (
    <section className="menu-panel">
      <nav className="menu-categories" aria-label="Menu categories">
        {sampleCategories.map((category) => (
          <button
            key={category.id}
            onClick={() => setSelectedCategory(category.id)}
            className={selectedCategory === category.id ? '' : 'outline'}
            aria-pressed={selectedCategory === category.id}
          >
            {category.name}
          </button>
        ))}
      </nav>

      <div className="menu-items" role="grid" aria-label="Menu items">
        {filteredItems.map((item) => (
          <button
            key={item.id}
            className="menu-item-button"
            onClick={() => addItem(item)}
            aria-label={`Add ${item.name} at ${formatCurrency(item.price)}`}
          >
            <span className="item-name">{item.name}</span>
            <span className="item-price">{formatCurrency(item.price)}</span>
          </button>
        ))}
      </div>
    </section>
  )
}
