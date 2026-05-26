# Controllers

This folder contains the plugin's REST API controllers, automatically exposed by Jellyfin's ASP.NET Core routing system.

---

## Files

### `NotificationController.cs`

Main HTTP entry point for the plugin. Exposes the following endpoints:

| Endpoint | Method | Auth | Description |
|----------|---------|------|-------------|
| `/Notification/Send` | POST | Admin | Creates and pushes a notification |
| `/Notification/List` | GET | User | Returns the current user's notifications |
| `/Notification/MarkAsRead/{id}` | POST | User | Marks a notification as read |
| `/Notification/Admin/History` | GET | Admin | Full history for the dashboard |

**Technical points:**
- Authentication relies on Jellyfin claims (`Jellyfin-UserId` in priority, with fallback `NameIdentifier` / `sub`)
- Input validation is strict: title ≤ 120 chars, message ≤ 2000 chars, TargetUserId = `All` or valid GUID
- The controller contains **no business logic** — everything is delegated to `NotificationService`

### `ClientScriptController.cs`

Serves the client JavaScript file (`notif-client.js`) from the DLL's embedded resources.

| Endpoint | Method | Auth | Description |
|----------|---------|------|-------------|
| `/JellyNotif/client` | GET | Public | Client JS script (1h cache) |

**Technical points:**
- The script is cached in a static `Lazy<byte[]>` (only one read via reflection on first call)
- `ResponseCache(Duration = 3600)` on the HTTP side to prevent repeated requests
- `AllowAnonymous` because the script is loaded before any user authentication

---

## Conventions

- Inherit from `ControllerBase` (not `Controller` — no MVC Views)
- Base route: `[Route("")]` → routes defined by attribute on each method
- `[Authorize(Policy = "RequiresElevation")]` = admin only
- `ConfigureAwait(false)` on all `await`
