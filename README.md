# ucode

Full stack scaffold: backend (ASP.NET Core 10) on port 5001, frontend (React + Vite) served by nginx with `/api` proxy to backend. External SSL/domain will sit in front (`ucode.ava-kk.ru`).

## Backend setup
- Config via `.env` (used by docker-compose) or env vars: `Telegram__BotToken`, `Jwt__SigningKey`, optional `Jwt__Issuer`, `Jwt__Audience`, `Jwt__LifetimeMinutes`, `ConnectionStrings__Default` (Postgres).
- Run locally: `cd backend && ConnectionStrings__Default="Host=...;Port=5432;Database=ucode;Username=...;Password=..." dotnet run` (or add to `.env`).
- Run in Docker: `docker compose up --build` (frontend host port via `FRONTEND_PORT` env, default 5080; backend is only exposed inside the compose network as `ucode_backend:5001`; Postgres service `db` included).
- Endpoints: `POST /auth/telegram` with `{"initData":"<window.Telegram.WebApp.initData>"}` returns token/expiresAt/user; `GET /auth/me` with `Authorization: Bearer <token>` returns user; `GET /health` for liveness.

## Auth flow notes
- Validates Telegram WebApp initData: builds `data_check_string`, `secret_key = HMAC_SHA256("WebAppData", bot_token)`, compares HMAC-SHA256 hash from Telegram.
- Issues JWT signed with `Jwt:SigningKey`. Tokens are cached in-memory per Telegram user id; repeated auth reuses the same token until it expires. Frontend should keep it in browser memory only.

## Frontend
- Vite + React + TypeScript (`frontend/`). Uses `/api` base path (proxy via nginx) or override with `VITE_API_BASE`.
- UI автоподхватывает `window.Telegram.WebApp.initData`, вызывает `/auth/telegram`, хранит токен в памяти, и `/auth/me` для проверки (кнопка «Проверить токен», отображает exp).
- Dockerfile serves built assets via nginx; `frontend/nginx.conf` proxies `/api` → `backend:5001`.

## Next steps
- Point outer nginx (SSL + domain) to the frontend service; it will handle static + `/api` proxy to backend.
- Add CI/build checks when Node/.NET are installed locally.
