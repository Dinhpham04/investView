# Docker Deployment

This setup runs InvestView on one VPS with Docker Compose:

- `web`: Nginx serving the Vite build and proxying `/health`, `/api/`, and `/hubs/` to the API.
- `api`: ASP.NET Core API on port `8080` inside the Docker network.
- `sqlserver`: SQL Server 2022 with a persistent volume.
- `redis`: Redis for market state and pub/sub.

## Environment

Create `.env` from `.env.example` and set production values before starting the stack. Required values:

- `SQL_SA_PASSWORD`: strong SQL Server SA password. Avoid semicolons because the value is embedded in a connection string.
- `JWT_SIGNING_KEY`: stable signing key with at least 32 characters. Changing it invalidates existing JWTs.
- `INVESTVIEW_DEMO_PASSWORD`: password for the seeded demo account.

For DNSE market data, set:

```env
MARKET_DATA_PROVIDER=Dnse
QUOTE_STREAM_SOURCE_PROVIDER=DnseWebSocket
SECURITY_DEFINITION_WARMUP_ENABLED=true
DNSE_API_KEY=your-dnse-api-key
DNSE_API_SECRET=your-dnse-api-secret
```

Keep `MARKET_DATA_PROVIDER=Mock` and `QUOTE_STREAM_SOURCE_PROVIDER=Mock` when DNSE credentials are not available.

## Commands

Validate Compose interpolation:

```powershell
docker compose --env-file .env config
```

Build images:

```powershell
docker compose --env-file .env build
```

Start the stack:

```powershell
docker compose --env-file .env up -d
```

Check status and logs:

```powershell
docker compose ps
docker compose logs -f api
```

By default the web app is exposed on `http://localhost:8080`, while the API is only bound to `127.0.0.1:5122` for host-local diagnostics.

## VPS Reverse Proxy

For a domain on the VPS, terminate TLS at the host reverse proxy and forward traffic to `127.0.0.1:${WEB_HTTP_PORT}`. The host proxy must send:

```nginx
proxy_set_header X-Forwarded-Proto $scheme;
proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
proxy_set_header Host $host;
```

The web container preserves `X-Forwarded-Proto` when proxying API and SignalR traffic, and the API already processes forwarded headers before HTTPS redirection.
