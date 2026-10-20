import { useEffect, useState } from 'react'
import { apiFetch, login, logout, resume, LoggedOut } from './api'

type Order = { id: number; item: string; status: string }

export default function App() {
  const [signedIn, setSignedIn] = useState(false)
  const [orders, setOrders] = useState<Order[]>([])
  const [error, setError] = useState('')

  useEffect(() => {
    resume().then(setSignedIn)
  }, [])

  async function signIn(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const form = new FormData(e.currentTarget)
    const ok = await login(String(form.get('username')), String(form.get('password')))
    setSignedIn(ok)
    setError(ok ? '' : 'Wrong username or password')
  }

  async function loadOrders() {
    try {
      const res = await apiFetch('/api/orders')
      setOrders((await res.json()).orders)
      setError('')
    } catch (err) {
      if (!(err instanceof LoggedOut)) throw err
      setSignedIn(false)
      setOrders([])
      setError('Your session ended. Sign in again.')
    }
  }

  async function signOut() {
    await logout()
    setSignedIn(false)
    setOrders([])
  }

  if (!signedIn)
    return (
      <main>
        <h1>Orders</h1>
        <form onSubmit={signIn}>
          <input name="username" defaultValue="dev" aria-label="Username" />
          <input name="password" type="password" defaultValue="pass" aria-label="Password" />
          <button>Sign in</button>
        </form>
        {error && <p className="error">{error}</p>}
      </main>
    )

  return (
    <main>
      <h1>Orders</h1>
      <div className="row">
        <button onClick={loadOrders}>Load orders</button>
        <button onClick={signOut}>Sign out</button>
      </div>
      <ul>
        {orders.map((o) => (
          <li key={o.id}>
            #{o.id} {o.item} <span>{o.status}</span>
          </li>
        ))}
      </ul>
      {error && <p className="error">{error}</p>}
    </main>
  )
}
