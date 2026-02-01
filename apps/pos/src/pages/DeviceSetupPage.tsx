import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useDeviceAuth } from '../contexts/DeviceAuthContext'

interface DeviceCodeState {
  userCode: string
  verificationUri: string
  expiresIn: number
  requestedAt: number
}

export default function DeviceSetupPage() {
  const { requestDeviceCode, pollForToken, stopPolling, isDeviceAuthenticated, error } = useDeviceAuth()
  const [deviceCode, setDeviceCode] = useState<DeviceCodeState | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [timeRemaining, setTimeRemaining] = useState<number>(0)
  const navigate = useNavigate()

  useEffect(() => {
    if (isDeviceAuthenticated) {
      navigate('/login', { replace: true })
    }
  }, [isDeviceAuthenticated, navigate])

  // Countdown timer
  useEffect(() => {
    if (!deviceCode) return

    const elapsed = Math.floor((Date.now() - deviceCode.requestedAt) / 1000)
    const remaining = Math.max(0, deviceCode.expiresIn - elapsed)
    setTimeRemaining(remaining)

    if (remaining <= 0) {
      setDeviceCode(null)
      stopPolling()
      return
    }

    const timer = setInterval(() => {
      const elapsed = Math.floor((Date.now() - deviceCode.requestedAt) / 1000)
      const remaining = Math.max(0, deviceCode.expiresIn - elapsed)
      setTimeRemaining(remaining)

      if (remaining <= 0) {
        setDeviceCode(null)
        stopPolling()
      }
    }, 1000)

    return () => clearInterval(timer)
  }, [deviceCode, stopPolling])

  async function handleRequestCode() {
    setIsLoading(true)
    try {
      const response = await requestDeviceCode()
      setDeviceCode({
        userCode: response.userCode,
        verificationUri: response.verificationUri,
        expiresIn: response.expiresIn,
        requestedAt: Date.now(),
      })
      pollForToken(response.deviceCode, response.userCode, response.interval)
    } catch {
      // Error handled by context
    } finally {
      setIsLoading(false)
    }
  }

  function formatTime(seconds: number): string {
    const mins = Math.floor(seconds / 60)
    const secs = seconds % 60
    return `${mins}:${secs.toString().padStart(2, '0')}`
  }

  if (deviceCode) {
    return (
      <main className="container" style={{ maxWidth: '600px', marginTop: '10vh' }}>
        <article style={{ textAlign: 'center', padding: '2rem' }}>
          <header style={{ marginBottom: '2rem' }}>
            <h1 style={{ marginBottom: '0.5rem' }}>Authorize This Device</h1>
            <p style={{ color: 'var(--pico-muted-color)' }}>
              Complete setup on your phone or computer
            </p>
          </header>

          <section style={{ marginBottom: '2rem' }}>
            <p style={{ marginBottom: '1rem' }}>1. On another device, go to:</p>
            <p style={{
              fontSize: '1.25rem',
              fontWeight: 600,
              backgroundColor: 'var(--pico-card-background-color)',
              padding: '1rem',
              borderRadius: 'var(--pico-border-radius)',
              marginBottom: '2rem'
            }}>
              {deviceCode.verificationUri}
            </p>

            <p style={{ marginBottom: '1rem' }}>2. Enter this code:</p>
            <p style={{
              fontSize: '2.5rem',
              fontWeight: 700,
              fontFamily: 'monospace',
              letterSpacing: '0.15em',
              backgroundColor: 'var(--pico-primary-background)',
              color: 'var(--pico-primary-inverse)',
              padding: '1.5rem 2rem',
              borderRadius: 'var(--pico-border-radius)',
              display: 'inline-block',
            }}>
              {deviceCode.userCode}
            </p>
          </section>

          <footer>
            <p style={{ color: 'var(--pico-muted-color)', marginBottom: '1rem' }}>
              Code expires in {formatTime(timeRemaining)}
            </p>
            <progress style={{ width: '100%' }} />
            <p style={{ fontSize: '0.875rem', color: 'var(--pico-muted-color)' }}>
              Waiting for authorization...
            </p>
            {error && (
              <p role="alert" style={{ color: 'var(--pico-del-color)', marginTop: '1rem' }}>
                {error}
              </p>
            )}
          </footer>
        </article>
      </main>
    )
  }

  return (
    <main className="container" style={{ maxWidth: '500px', marginTop: '15vh' }}>
      <article style={{ textAlign: 'center', padding: '2rem' }}>
        <header style={{ marginBottom: '2rem' }}>
          <h1 style={{ marginBottom: '0.5rem' }}>DarkVelocity POS</h1>
          <p style={{ color: 'var(--pico-muted-color)' }}>Device Setup</p>
        </header>

        <p style={{ marginBottom: '2rem' }}>
          This device needs to be authorized before it can be used.
          You'll need access to an authorized account to complete setup.
        </p>

        <button
          onClick={handleRequestCode}
          disabled={isLoading}
          aria-busy={isLoading}
          style={{ width: '100%' }}
        >
          {isLoading ? 'Getting code...' : 'Begin Device Setup'}
        </button>

        {error && (
          <p role="alert" style={{ color: 'var(--pico-del-color)', marginTop: '1rem' }}>
            {error}
          </p>
        )}
      </article>
    </main>
  )
}
