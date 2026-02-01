import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useStation } from '../contexts/StationContext'
import { useDeviceAuth } from '../contexts/DeviceAuthContext'

export default function StationSelectPage() {
  const { stations, selectedStation, isLoading, error, selectStation, loadStations } = useStation()
  const { isDeviceAuthenticated, clearDeviceAuth } = useDeviceAuth()
  const navigate = useNavigate()

  useEffect(() => {
    if (!isDeviceAuthenticated) {
      navigate('/setup', { replace: true })
    }
  }, [isDeviceAuthenticated, navigate])

  useEffect(() => {
    if (selectedStation) {
      navigate('/display', { replace: true })
    }
  }, [selectedStation, navigate])

  async function handleSelectStation(station: { id: string; name: string; orderTypes: string[] }) {
    await selectStation(station)
  }

  function handleResetDevice() {
    clearDeviceAuth()
    navigate('/setup', { replace: true })
  }

  if (isLoading && stations.length === 0) {
    return (
      <main className="container" style={{ maxWidth: '600px', marginTop: '10vh' }}>
        <article style={{ textAlign: 'center', padding: '2rem' }} aria-busy="true">
          <p>Loading stations...</p>
        </article>
      </main>
    )
  }

  return (
    <main className="container" style={{ maxWidth: '600px', marginTop: '5vh' }}>
      <article style={{ padding: '2rem' }}>
        <header style={{ textAlign: 'center', marginBottom: '2rem' }}>
          <h1 style={{ marginBottom: '0.5rem' }}>Select Station</h1>
          <p style={{ color: 'var(--pico-muted-color)' }}>
            Choose which kitchen station this display will show orders for
          </p>
        </header>

        {error && (
          <p role="alert" style={{ color: 'var(--pico-del-color)', marginBottom: '1rem', textAlign: 'center' }}>
            {error}
          </p>
        )}

        <section style={{ display: 'grid', gap: '1rem' }}>
          {stations.map((station) => (
            <button
              key={station.id}
              onClick={() => handleSelectStation(station)}
              disabled={isLoading}
              style={{
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'flex-start',
                padding: '1.5rem',
                textAlign: 'left',
              }}
            >
              <strong style={{ fontSize: '1.25rem', marginBottom: '0.5rem' }}>
                {station.name}
              </strong>
              <small style={{ color: 'var(--pico-muted-color)' }}>
                Order types: {station.orderTypes.join(', ')}
              </small>
            </button>
          ))}
        </section>

        {stations.length === 0 && !isLoading && (
          <p style={{ textAlign: 'center', color: 'var(--pico-muted-color)' }}>
            No stations available. Please configure stations in the back office.
          </p>
        )}

        <footer style={{ marginTop: '2rem', textAlign: 'center' }}>
          <button
            onClick={() => loadStations()}
            disabled={isLoading}
            className="secondary"
            style={{ marginRight: '1rem' }}
          >
            Refresh
          </button>
          <button
            onClick={handleResetDevice}
            className="outline"
          >
            Reset Device
          </button>
        </footer>
      </article>
    </main>
  )
}
