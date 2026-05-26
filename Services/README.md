# Services

This folder contains the business layer of the plugin — the notification engine.

---

## Files

### `NotificationService.cs`

Singleton service registered via `PluginServiceRegistrar`. Implements the **Outbox pattern**:

```
SendAsync()
  ├── (1) INSERT in database (persist)    → NotificationRepository
  ├── (2) Push WebSocket (native toast)   → ISessionManager
  └── (3) UPDATE IsSent = true            → NotificationRepository
```

**Responsibilities:**
- Orchestrate the persist → push → mark sent flow
- Resolve `ISessionManager` lazily (unavailable at `RegisterServices()`)
- Target active sessions of the user or broadcast to all (`"All"`)
- Delegate all SQLite access to the `NotificationRepository`

**Technical points:**

| Concept | Implementation |
|---------|---------------|
| Lazy init thread-safe | `volatile` + `Interlocked.CompareExchange` on `_sessionManager` |
| WebSocket Push | `ISessionManager.SendMessageCommand()` per session |
| Targeting | `"All"` → all active sessions / GUID → sessions of this user |
| Logging | `ILogger<NotificationService>` with `[JellyNotif]` prefix |

**Must NOT contain:**
- Direct SQL (→ Repository)
- HTTP validation (→ Controller)
- Display logic (→ Client JS)

---

## Dependency Injection

```csharp
services.AddSingleton<NotificationService>();
```

The service is resolved only once at startup. `ISessionManager` is resolved on first access (lazy) because the Jellyfin server has not registered it yet at `RegisterServices()` time.
