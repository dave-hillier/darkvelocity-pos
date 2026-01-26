import { useState } from 'react'

interface PinPadProps {
  onSubmit: (pin: string) => void
  isLoading?: boolean
  error?: string | null
  maxLength?: number
}

export default function PinPad({ onSubmit, isLoading, error, maxLength = 4 }: PinPadProps) {
  const [pin, setPin] = useState('')

  function handleDigit(digit: string) {
    if (pin.length < maxLength) {
      const newPin = pin + digit
      setPin(newPin)
      if (newPin.length === maxLength) {
        onSubmit(newPin)
      }
    }
  }

  function handleBackspace() {
    setPin((prev) => prev.slice(0, -1))
  }

  function handleClear() {
    setPin('')
  }

  const displayValue = '*'.repeat(pin.length)

  return (
    <div>
      <div className="pin-display" aria-live="polite">
        {displayValue || <span style={{ opacity: 0.3 }}>Enter PIN</span>}
      </div>

      {error && (
        <p role="alert" style={{ color: 'var(--pico-del-color)', textAlign: 'center' }}>
          {error}
        </p>
      )}

      <div className="pin-keypad">
        {['1', '2', '3', '4', '5', '6', '7', '8', '9'].map((digit) => (
          <button
            key={digit}
            type="button"
            onClick={() => handleDigit(digit)}
            disabled={isLoading}
            aria-label={digit}
          >
            {digit}
          </button>
        ))}
        <button
          type="button"
          onClick={handleClear}
          disabled={isLoading}
          className="secondary"
          aria-label="Clear"
        >
          C
        </button>
        <button
          type="button"
          onClick={() => handleDigit('0')}
          disabled={isLoading}
          aria-label="0"
        >
          0
        </button>
        <button
          type="button"
          onClick={handleBackspace}
          disabled={isLoading}
          className="secondary"
          aria-label="Backspace"
        >
          &larr;
        </button>
      </div>
    </div>
  )
}
