import { useState, useEffect } from 'react'
import { useOrder } from '../contexts/OrderContext'
import { useMenu } from '../contexts/MenuContext'

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-GB', {
    style: 'currency',
    currency: 'GBP',
  }).format(amount)
}

export default function MenuPanel() {
  const { categories, items, isLoading } = useMenu()
  const [selectedCategory, setSelectedCategory] = useState<string | null>(null)
  const { addItem, keypadValue } = useOrder()

  const multiplierQty = keypadValue ? parseInt(keypadValue, 10) : null
  const showMultiplier = multiplierQty !== null && !isNaN(multiplierQty) && multiplierQty > 0

  // Set initial category when categories load
  useEffect(() => {
    if (categories.length > 0 && !selectedCategory) {
      setSelectedCategory(categories[0].id)
    }
  }, [categories, selectedCategory])

  const filteredItems = selectedCategory
    ? items.filter((item) => item.categoryId === selectedCategory)
    : items

  if (isLoading && categories.length === 0) {
    return (
      <section className="menu-panel" aria-busy="true">
        <p>Loading menu...</p>
      </section>
    )
  }

  return (
    <section className="menu-panel">
      {showMultiplier && (
        <div className="quantity-multiplier" aria-live="polite">
          Adding {multiplierQty}x items
        </div>
      )}

      <nav className="menu-categories" aria-label="Menu categories">
        {categories.map((category) => (
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
            aria-label={`Add ${showMultiplier ? `${multiplierQty}x ` : ''}${item.name} at ${formatCurrency(item.price)}`}
          >
            <span className="item-name">{item.name}</span>
            <span className="item-price">{formatCurrency(item.price)}</span>
          </button>
        ))}
      </div>
    </section>
  )
}
