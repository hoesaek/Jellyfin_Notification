# CLAUDE.md — Jellyfin Notification Plugin

Reference guide for AI interventions on this repository.

---

## Build and deployment commands

```bash
# Standard build (Release, net9.0)
dotnet build -c Release

# Full deployment (build + stop Jellyfin + copy DLL + inject index.html)
# Run as administrator (PowerShell)
.\deploy.ps1 -Version "X.X.X.X"

# Build only, without deployment
dotnet build -c Release --no-restore
```

> The `deploy.ps1` executes in order:
> 1. `dotnet build -c Release`
> 2. Stop Jellyfin service
> 3. Delete old versions in `C:\ProgramData\Jellyfin\Server\plugins\`
> 4. Copy DLL + meta.json to `Jellyfin_notification_{version}`
> 5. Inject `<script>` into `index.html` of the web client

---

## Project architecture

```
Jellyfin_notification/
├── Plugin.cs                          # Entry point — script injection, IHasWebPages
├── PluginServiceRegistrar.cs          # DI registration (Singleton for all services)
│
├── Controllers/
│   ├── NotificationController.cs      # REST API: Send, List, MarkAsRead, Admin/History
│   └── ClientScriptController.cs      # Serves embedded JS (/JellyNotif/client)
│
├── Services/
│   └── NotificationService.cs         # Business logic — Outbox pattern (persist → push → mark)
│
├── Data/
│   ├── NotificationDbContext.cs        # SQLite schema init + connection factory
│   └── NotificationRepository.cs      # Async SQLite CRUD (INSERT, SELECT, UPDATE, DELETE)
│
├── Models/
│   └── NotificationModels.cs          # NotificationEntity, DTOs (NotificationDto, AdminNotificationDto),
│                                      # SendNotificationRequest
│
├── Configuration/
│   └── PluginConfiguration.cs         # MaxNotifications, RetentionDays
│
├── ClientScript/
│   └── notif-client.js                # Client SPA — bell, panel, modal, polling
│
├── Pages/
│   ├── send.html                      # Admin page: send form
│   └── history.html                   # Admin page: history
│
├── deploy.ps1                         # Automated deployment script
├── meta.json                          # Plugin NuGet/Jellyfin metadata
└── Jellyfin_notification.csproj         # .NET 9 Project — class library
```

### Data flow

```
Admin (send.html)
    │
    ▼
POST /Notification/Send  ──→  NotificationController
    │                              │
    │                              ▼
    │                         NotificationService.SendAsync()
    │                              │
    │                    ┌─────────┴─────────┐
    │                    ▼                   ▼
    │            (B) INSERT SQLite    (C) Push WebSocket
    │             via Repository      via ISessionManager
    │                    │                   │
    │                    ▼                   ▼
    │            (D) UPDATE IsSent    Native Jellyfin toast
    │                                        │
    ▼                                        ▼
Client (notif-client.js)              Displayed immediately
    │
    ▼
GET /Notification/List  ←── Polling 60s (Outbox fallback)
    │
    ▼
Bell panel
```

---

## Key files responsibilities

| File | Responsibility | Must NOT contain |
|---------|---------------|---------------------|
| `Plugin.cs` | Bootstrap, `<script>` injection, admin pages | Business logic, DB access |
| `NotificationController.cs` | HTTP validation, routing, claims extraction | Business logic, SQL |
| `NotificationService.cs` | Outbox (persist → push → mark), purge | Direct SQL, HTTP access |
| `NotificationRepository.cs` | Pure SQLite CRUD, parameterized queries | Business logic, WebSocket |
| `NotificationDbContext.cs` | Schema, connection factory | Queries, logic |
| `notif-client.js` | UI bell/panel/modal, API polling | Server logic, SQL |
| `send.html` | Admin form, fetch calls | DB access, global DOM modifications |

---

## Code conventions

### C# — Backend

- **Namespace**: `Jellyfin_notification.{Folder}` (e.g.: `Jellyfin_notification.Services`)
- **Logging**: Always prefix with `[JellyNotif]` — e.g.: `_logger.LogInformation("[JellyNotif] ...")`
- **Async**: All I/O methods must be `async Task<T>` with `CancellationToken ct` as last parameter
- **ConfigureAwait**: Always `.ConfigureAwait(false)` on `await` (no synchronization context)
- **SQL**: Parameterized queries only (`$param`). NEVER string concatenation in queries
- **DI**: Services are Singleton. If a Jellyfin dependency is unavailable at startup, use lazy resolution via `IServiceProvider`
- **DTOs**: Always decorate with `[JsonPropertyName("camelCase")]` for an explicit API contract
- **XML Docs**: Mandatory on public methods and classes
- **Nullability**: `<Nullable>enable</Nullable>` — explicitly use `?`, check for nulls
- **Versioning**: Synchronize `Version` in `.csproj` AND `meta.json`

### JavaScript — Frontend

- **IIFE**: All code inside `(function () { 'use strict'; ... })();`
- **Console prefix**: `[JellyNotif]` on all `console.log/warn/error`
- **XSS Escaping**: Use `escHtml()` for ANY server data inserted into DOM via `innerHTML`
- **textContent**: Prefer `textContent` over `innerHTML` when no markup is needed
- **ApiClient**: Use `window.ApiClient.ajax()` with relative paths (never absolute URLs — token is injected automatically)
- **Authentication**: Always verify `getCurrentUserId()` before calling endpoints
- **Polling**: `POLL_INTERVAL = 60_000` (60s). Do not go below

---

## Security rules

### Jellyfin Authentication

- **User Claim**: Jellyfin 10.11.x uses `"Jellyfin-UserId"` (custom claim). Always search in this order:
  1. `User.FindFirst("Jellyfin-UserId")`
  2. `User.FindFirst(ClaimTypes.NameIdentifier)`
  3. `User.FindFirst("sub")`
- **Admin**: Admin endpoints use `[Authorize(Policy = "RequiresElevation")]` (Jellyfin native)
- **User**: User endpoints use `[Authorize]` + extraction of UserId claim
- **Client script**: `[AllowAnonymous]` because `<script>` is loaded before auth. The script contains no sensitive data

### Data validation

- **Title**: max 120 characters, required, trimmed
- **Message**: max 2000 characters, required, trimmed
- **TargetUserId**: must be `"All"` or a valid GUID (Guid.TryParse)
- **Type**: strict whitelist `{ "Info", "Warning", "Alert" }` — any other value → `"Info"` fallback
- **notifId**: constrained by route template `{notifId:guid}` — invalid = automatic 404

### XSS Prevention

- **Backend**: JSON responses are natively escaped by `System.Text.Json`
- **Frontend (notif-client.js)**: `escHtml()` escapes `& < > " '` before any `innerHTML`
- **Frontend (send.html)**: `showFeedback()` uses `textContent` (no innerHTML with user data)
- **Server errors**: Truncated to 200 characters before client display

### SQLite

- **Parameters**: Always `$param` in queries, never concatenation
- **Connection string**: Built via `SqliteConnectionStringBuilder` (no interpolation — injection prevention)
- **WAL mode**: Enabled at startup for read/write concurrency
- **Per-operation connection**: Each repository method opens and disposes its own connection

### Lazy DI Resolution

`ISessionManager` is not available in DI container at `RegisterServices()` time (Jellyfin services are registered after plugins). Solution:
- Inject `IServiceProvider` instead of `ISessionManager`
- Resolve via a thread-safe property with `Interlocked.CompareExchange`
- Resolution is amortized (only once, then cached)

---

## Files NOT to modify

| File | Reason |
|---------|--------|
| `index.html` (Jellyfin) | Dynamically modified by `Plugin.cs` at startup |

---

## Common errors and solutions

| Symptom | Cause | Solution |
|----------|-------|----------|
| 500 `Unable to resolve ISessionManager` | DI registration order | Lazy resolution via IServiceProvider |
| 401 on `GET /Notification/List` | Wrong UserId claim | Search `"Jellyfin-UserId"` first |
| `SessionMessageType.Message` compile error | Enum non-existent in 10.11.8 | Use `SendMessageCommand` per session |
| Client script not loaded | `<script>` not injected | Check `deploy.ps1` step 5, or `Plugin.cs` |
| First poll is 401 | `tryInit()` without checking auth | Require non-null `getCurrentUserId()` |

---

## Compatibility

- **Jellyfin**: 10.11.x (targetAbi: `10.11.0.0`)
- **.NET**: 9.0
- **SQLite**: Provided by Jellyfin runtime (no native DLL to embed)
- **Browsers**: All browsers supported by Jellyfin web client
