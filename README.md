# JWT access and refresh tokens: what actually happens when the token expires

A .NET 10 minimal API and a Vite React app that show the whole refresh flow on the wire.

- Access token: a JWT, 60 seconds (so you can watch it expire), kept in memory in the React app.
- Refresh token: 32 random bytes in an `HttpOnly; Secure; SameSite=Strict; Path=/auth` cookie, 7 days, rotated on every use.
- Reuse detection: present a refresh token that was already swapped and the API revokes its whole family.

## Run it

```bash
cd api && dotnet run          # http://localhost:5080
cd web && npm install && npm run dev   # http://localhost:5173
```

Sign in as `dev` / `pass`, click **Load orders**, wait a minute, click again, and watch DevTools › Network:
`GET /api/orders 401` → `POST /auth/refresh 200` → `GET /api/orders 200`.

## Files worth reading

- `api/Program.cs`: the endpoints, the cookie, `ClockSkew = TimeSpan.Zero` (the default of 5 minutes would let a 60 s token live 6).
- `api/Tokens.cs`: the refresh-token register, rotation and reuse detection.
- `web/src/api.ts`: the fetch wrapper: catch the 401, refresh once, retry.

`/debug/register` (Development only) lists the register so you can see rotation happen. Demo code: the users, the signing key and the in-memory register are not for production.
