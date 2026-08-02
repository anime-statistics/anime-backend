# anime-backend

Бэкенд персонального трекера аниме и манги. Реализует контракт фронтенда
[`anime-frontend`](https://github.com/anime-statistics/anime-frontend) — моки
фронтенда (`src/mocks/handlers/*.ts`, `src/apis/dtos/*.ts`) являются исполняемой
спецификацией, выжимка лежит в `anime-frontend-ai-agents/BACKEND_PROMPT.md`.

## Стек

- **ASP.NET Core / .NET 10** — контроллеры, SignalR, статика вложений
- **EF Core 10 + SQLite** — хранение коллекции; миграции применяются на старте
- **Serilog** — консоль + rolling-файлы в `logs/`
- **NSwag** — Swagger UI на `/swagger`, ReDoc на `/redoc`, спека `/swagger/v1/swagger.json`
- **HybridCache + Microsoft.Extensions.Http.Resilience** — кэш и ретраи для внешних API

Без MediatR: хендлеры — обычные классы, контроллеры получают их из DI напрямую.
Мини-диспетчер доменных событий (`Application/Events`) — задел под будущий
движок триггеров, подписчиков пока ноль.

## Архитектура

```
src/
├─ AnimeBackend.Domain          # сущности, MediaId, merge-инварианты; ноль зависимостей
├─ AnimeBackend.Application     # хендлеры, DTO контракта, движок склейки поиска
├─ AnimeBackend.Infrastructure  # EF Core, клиенты Shikimori/AniLiberty, файлы
└─ AnimeBackend.Api             # контроллеры /api/v1, middleware ошибок, SignalR-хаб
tests/
├─ AnimeBackend.Application.Tests  # merge-движок, MediaId, доменные правила
└─ AnimeBackend.Api.Tests          # интеграционные: реальный хост + фейковые источники
```

Ключевая идея хранения: **БД — это только коллекция**. Каталог не хранится:
`GET /search` — прокси к Shikimori и AniLiberty со склейкой дубликатов на лету
(алгоритм и веса портированы из фронтового мока `mergeResults.ts`, порог 0.7).
Запись материализуется локально в момент, когда пользователь впервые её трогает
(первый тег, прогресс или заметка), и не удаляется при снятии последнего тега —
библиотека просто фильтрует по непустым `my_tags`.

## Запуск

```bash
dotnet run --project src/AnimeBackend.Api
```

API поднимется на `http://localhost:5000`, база `data/anime.db` и папка
вложений создадутся сами, шесть стартовых тегов засеются при первой миграции.

Связка с фронтендом: vite-прокси уже настроен (`/api → localhost:5000`), поэтому

```bash
pnpm --dir ../anime-frontend dev:api
```

и фронт на `http://localhost:3000` ходит в этот бэкенд.

## Конфигурация

`appsettings.json`, секции:

- `Sources:Shikimori` — `BaseUrl`, `UserAgent` (Shikimori отвергает анонимные
  клиенты; поставьте своё имя приложения), `SearchLimit`
- `Sources:AniLiberty` — `BaseUrl` (API), `AssetsBaseUrl` (постеры), `CatalogPageSize`
- `Storage` — путь вложений заметок и публичный префикс
- `Cors:Origins` — origin'ы фронтенда для dev без прокси

## Поведение, продиктованное контрактом

- На проводе только `snake_case`; null-поля не сериализуются
- Любая ошибка — `{ "message": "..." }`; бизнес-ошибки только 4xx, потому что
  429/5xx фронтенд ретраит трижды; недоступность источника — 424
- Недоступный источник в поиске деградирует молча (отдаём живые), все мёртвые — 424
- `/ai/*` и `/sync/*` — честные стабы: список моделей пуст, очередь
  синхронизации пуста, проверка интеграции реально пингует источник

## Тесты

```bash
dotnet test
```

Интеграционные поднимают полный хост на in-memory SQLite с фейковыми
источниками: склейка, наложение тегов, материализация, bulk-операции (50 работ
за один запрос), пагинация, формат ошибок.

## Docker

```bash
docker compose -f deploy/compose.yaml up --build
```

Порт 5000, данные в `./data`.

## Не реализовано (осознанно)

Триггеры на тегах, синхронизация со списками Shikimori, аутентификация,
вложения заметок как отдельная модель — по контракту они «не спроектированы,
обсудить до реализации». Точки расширения на месте: доменные события
`TagsChanged`/`ProgressChanged`, SignalR-хаб `/hubs/notifications` с контрактом
`LibraryUpdated`/`TriggerFired`, `FetchedAt` на снимках под будущий крон.

## Известное ограничение

С некоторых сетей `shikimori.one` рвёт TLS-хендшейк (антибот DDoS-Guard).
Бэкенд это переживает: поиск отдаёт живые источники, карточки Shikimori-работ
открываются из локальных снимков. `BaseUrl` источника конфигурируем — при
необходимости укажите зеркало или прокси.
