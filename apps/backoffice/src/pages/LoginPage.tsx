import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'

export default function LoginPage() {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const navigate = useNavigate()

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setIsLoading(true)

    // Simulate login
    await new Promise((resolve) => setTimeout(resolve, 1000))
    setIsLoading(false)
    navigate('/dashboard')
  }

  return (
    <main className="login-page">
      <article className="login-card">
        <header>
          <h1>DarkVelocity</h1>
          <p>Back Office Login</p>
        </header>

        <form onSubmit={handleSubmit}>
          <label>
            Email
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="admin@example.com"
              required
              autoComplete="email"
            />
          </label>

          <label>
            Password
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="Enter password"
              required
              autoComplete="current-password"
            />
          </label>

          <button type="submit" disabled={isLoading} aria-busy={isLoading}>
            {isLoading ? 'Signing in...' : 'Sign In'}
          </button>
        </form>
      </article>
    </main>
  )
}
