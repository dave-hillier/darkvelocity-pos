import { useNavigate } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'
import PinPad from '../components/PinPad'

export default function LoginPage() {
  const { loginWithPin, isLoading, error } = useAuth()
  const navigate = useNavigate()

  async function handlePinSubmit(pin: string) {
    try {
      await loginWithPin(pin)
      navigate('/register')
    } catch {
      // Error is handled by context
    }
  }

  return (
    <main className="login-container">
      <header>
        <h1>DarkVelocity POS</h1>
      </header>

      <article>
        <header>
          <h2>Enter PIN</h2>
        </header>

        <PinPad
          onSubmit={handlePinSubmit}
          isLoading={isLoading}
          error={error}
        />

        <footer>
          <small>Scan QR code or enter your 4-digit PIN</small>
        </footer>
      </article>
    </main>
  )
}
