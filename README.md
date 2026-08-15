# ucode

Telegram Mini App для промо-акции ПК-клуба: при покупке леденца кассир на кассе генерировал одноразовый код на N баллов (жил 40 минут), покупатель открывал мини-приложение в Telegram, вводил код и получал баллы на свой счёт. Баллы суммировались в live-лидерборд участников акции.

Full stack: backend (ASP.NET Core) на порту 5001, frontend (React + Vite), отдаётся nginx с проксированием `/api` на backend. Снаружи домен/SSL (`ucode.ava-kk.ru`) вешался поверх.

## Как это работает

- **Кассир** авторизуется как `admin`/`root` и генерирует код (`POST /codes/admin/generate`) — случайные 5 символов, TTL 40 минут, привязан к количеству баллов за конкретную покупку.
- **Покупатель** открывает мини-апп из Telegram (авторизация через `window.Telegram.WebApp.initData`), вводит код (`POST /codes/redeem`) — код одноразовый, баллы начисляются на Telegram-аккаунт.
- **Лидерборд** (`GET /codes/leaderboard`) считается суммой баллов по использованным кодам на пользователя; обновления транслируются всем открытым мини-аппам через WebSocket (`LeaderboardNotifier`), а не по поллингу.

## Backend

- Конфиг — через `.env` (используется docker-compose) или переменные окружения: `Telegram__BotToken`, `Jwt__SigningKey`, опционально `Jwt__Issuer`, `Jwt__Audience`, `Jwt__LifetimeMinutes`, `ConnectionStrings__Default` (Postgres).
- Локально: `cd backend && ConnectionStrings__Default="Host=...;Port=5432;Database=ucode;Username=...;Password=..." dotnet run` (или прописать в `.env`).
- В Docker: `docker compose up --build` (порт фронтенда наружу — `FRONTEND_PORT`, по умолчанию 5080; backend доступен только внутри сети compose как `ucode_backend:5001`; Postgres поднимается тут же как сервис `db`).
- Эндпоинты: `POST /auth/telegram` с `{"initData": "<window.Telegram.WebApp.initData>"}` возвращает token/expiresAt/user; `GET /auth/me` с `Authorization: Bearer <token>` возвращает пользователя; `GET /health` для liveness.

### Авторизация

Валидирует Telegram WebApp `initData`: строится `data_check_string`, `secret_key = HMAC_SHA256("WebAppData", bot_token)`, сравнивается HMAC-SHA256 хэш от Telegram. Выдаётся JWT, подписанный `Jwt:SigningKey`; токены кэшируются в памяти по Telegram user id — повторная авторизация переиспользует тот же токен, пока он не истёк. На фронтенде токен хранится только в памяти браузера.

## Frontend

- Vite + React + TypeScript (`frontend/`). Ходит на `/api` (проксируется nginx) либо на адрес из `VITE_API_BASE`.
- UI сам подхватывает `window.Telegram.WebApp.initData`, вызывает `/auth/telegram`, хранит токен в памяти; кнопка «Проверить токен» дёргает `/auth/me` и показывает `exp`.
- Dockerfile собирает и отдаёт статику через nginx; `frontend/nginx.conf` проксирует `/api` → `backend:5001`.
