# Contributing to Jellyfin Notification

Thanks for your interest. Here are the guidelines.

---

## Dev setup

### Prerequisites

- [.NET SDK 9.0](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Jellyfin Server 10.11.x](https://jellyfin.org/downloads/)
- Editor with C# support (VS Code, Visual Studio, Rider)

### Install

```bash
git clone https://github.com/your-user/jellyfin-notification.git
cd jellyfin-notification
dotnet restore
dotnet build
```

### Test locally

```powershell
# Build + deploy + restart (Windows, admin PowerShell)
.\deploy.ps1 -Version "2.3.0.0"
```

---

## Code conventions

### C# — Backend

| Rule | Example |
|------|---------|
| Namespace = folder | `Jellyfin_notification.Services` |
| Log prefix | `[JellyNotif]` in all messages |
| Async + CancellationToken | `Task<T> DoAsync(..., CancellationToken ct)` |
| ConfigureAwait(false) | On all `await` |
| Parameterized SQL | `$param` — never concatenation |
| XML Docs | Required on all public classes/methods |
| JsonPropertyName | On all API-exposed DTOs |

### JavaScript — Frontend

| Rule | Example |
|------|---------|
| IIFE strict | `(function () { 'use strict'; ... })();` |
| Console prefix | `[JellyNotif]` |
| XSS prevention | `esc()` for any `innerHTML` with server data |
| textContent | Preferred over `innerHTML` when no markup needed |
| ApiClient | `window.ApiClient.ajax()` — never raw `fetch` in client script |
| Polling | `POLL_INTERVAL = 60_000` minimum |

---

## Git workflow

1. **Fork** the repo
2. **Create a branch** from `main`:
   ```bash
   git checkout -b feat/my-feature
   # or
   git checkout -b fix/my-bugfix
   ```
3. **Commit** with clear messages:
   ```
   feat: add email notification support
   fix: polling after disconnect
   docs: update README
   refactor: extract push service
   ```
4. **Build and test** before push:
   ```bash
   dotnet build -c Release
   ```
5. **Open a Pull Request** to `main`

### Commit prefixes

| Prefix | Usage |
|--------|-------|
| `feat:` | New feature |
| `fix:` | Bug fix |
| `docs:` | Documentation only |
| `refactor:` | Refactoring without functional change |
| `style:` | CSS, formatting, no logic impact |
| `perf:` | Performance optimization |
| `security:` | Security fix |
| `chore:` | Maintenance (CI, deps, scripts) |

---

## File structure responsibilities

| Layer | File(s) | Responsibility | Not for |
|-------|---------|---------------|---------|
| **Controller** | `NotificationController.cs` | HTTP validation, routing | Business logic, SQL |
| **Service** | `NotificationService.cs` | Outbox pattern, orchestration | Direct SQL, HTTP access |
| **Repository** | `NotificationRepository.cs` | SQLite CRUD | Business logic |
| **Models** | `NotificationModels.cs` | Entities, DTOs | Logic |
| **Client** | `notif-client.js` | Bell, panel, modal UI | Server logic |

---

## Security rules

Non-negotiable in any contribution:

- Never concatenate SQL
- Never use `innerHTML` with unescaped data
- Never commit secrets or tokens
- Always validate inputs server-side (length, GUID format)
- Always use `[Authorize]` or `[Authorize(Policy = "RequiresElevation")]`
- Always test with a non-admin account to verify guards

---

## Reporting bugs

Open an [issue](../../issues/new) with:
- Jellyfin version
- Plugin version
- Relevant Jellyfin logs (filter with `JellyNotif`)
- Browser console (F12) if frontend issue
- Steps to reproduce

---

## License

By contributing, you agree your contributions are distributed under the same [MIT License](LICENSE).
