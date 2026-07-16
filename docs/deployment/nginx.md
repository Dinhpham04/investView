# Nginx Reverse Proxy

InvestView uses two Nginx layers in production:

- Host Nginx on the VPS terminates public traffic for the domain.
- The `web` Docker container serves the Vite build and proxies `/health`, `/api/`, and `/hubs/` to the API container.

The host Nginx should proxy all requests to the Docker web port, which defaults to `127.0.0.1:8080`.

## Domain

Recommended domain for the current VPS:

```text
investview.automation.info.vn
```

Create or update the Cloudflare A record first:

```bash
cd automation
./scripts/cf-upsert-a-record.sh investview.automation.info.vn
```

Keep the record DNS-only while issuing Let's Encrypt certificates directly on the VPS.

## Install Host Nginx Config

Start the Docker stack first so Nginx has something to proxy to:

```bash
docker compose --env-file .env up -d
curl -f http://127.0.0.1:8080/health
```

Install the host Nginx reverse proxy from the local machine:

```bash
cd automation
INVESTVIEW_DOMAIN=investview.automation.info.vn \
WEB_HTTP_PORT=8080 \
bash ./scripts/install-investview-nginx.sh
```

This installs `/etc/nginx/sites-available/investview.conf` on the VPS and enables it.

## Enable TLS

After DNS resolves to the VPS, run:

```bash
cd automation
INVESTVIEW_DOMAIN=investview.automation.info.vn \
WEB_HTTP_PORT=8080 \
ISSUE_TLS=true \
LETSENCRYPT_EMAIL=you@example.com \
bash ./scripts/install-investview-nginx.sh
```

Certbot will edit the Nginx site to add the HTTPS server and HTTP-to-HTTPS redirect.

## Verify

On the VPS:

```bash
nginx -t
systemctl status nginx --no-pager
curl -f http://127.0.0.1:8080/health
curl -f https://investview.automation.info.vn/health
```

For SignalR, the host Nginx preserves `Upgrade` and `Connection` headers, then the web container forwards `/hubs/` to the API container.
