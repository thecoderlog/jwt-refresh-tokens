// The access token lives here, in memory. Not in localStorage, not in a cookie.
let accessToken: string | null = null

// One refresh at a time: if three calls get a 401 together, they all wait on the same one.
let refreshing: Promise<boolean> | null = null

export async function login(username: string, password: string) {
  const res = await fetch('/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username, password }),
  })
  if (!res.ok) return false
  accessToken = (await res.json()).accessToken
  return true
}

// On page load the token in memory is gone, but the cookie isn't: go to the desk first.
export const resume = () => refresh()

function refresh() {
  refreshing ??= fetch('/auth/refresh', { method: 'POST' })
    .then(async (res) => {
      accessToken = res.ok ? (await res.json()).accessToken : null
      return res.ok
    })
    .finally(() => (refreshing = null))
  return refreshing
}

export async function apiFetch(url: string, init: RequestInit = {}) {
  const send = () =>
    fetch(url, { ...init, headers: { ...init.headers, Authorization: `Bearer ${accessToken}` } })

  const res = await send()
  if (res.status !== 401) return res

  // The day pass ran out. Show the membership card at the desk, once, then try again.
  if (!(await refresh())) throw new LoggedOut()
  return send()
}

export async function logout() {
  await fetch('/auth/logout', { method: 'POST' })
  accessToken = null
}

export class LoggedOut extends Error {}
