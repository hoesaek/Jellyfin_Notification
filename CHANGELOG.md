# Changelog

All notable changes to this project are documented in this file.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).
This project uses [Semantic Versioning](https://semver.org/).

---

## [2.3.0] — 2026-05-26

### Added
- Client-side dismiss: individual delete button on each notification in the bell panel
- "Clear all" button in the panel header
- Dismiss persistence in `localStorage` (survives refresh)
- Character counters on the send page (title + message)
- Type selector pills (replaces `<select>`)
- Statistics bar in history (Total, Info, Warning, Alert)
- Spinner loader in history
- Sent indicator (green/grey dot) in history table
- Type tags in history table
- Delete button in detail modal
- Complete visual overhaul: clean, professional, Jellyfin-native design

### Changed
- Redesigned `send.html`: clean header, card-less layout, subtle feedback
- Redesigned `history.html`: stats bar, filter row, clean table
- Redesigned `notif-client.js`: minimal panel, clean modal, no decorative elements

### Removed
- Dead code: `Logger.cs` (replaced by `ILogger<T>`)
- All decorative emojis from UI and documentation

### Documentation
- Complete README for GitHub (features, architecture, API, security)
- CONTRIBUTING.md with code conventions and git workflow
- CHANGELOG.md (this file)
- LICENSE (MIT)
- .gitignore
- Per-module README.md for Controllers, Services, Data, Models, Pages, ClientScript, Configuration

## [2.2.0] — 2026-05-26

### Fixed
- **Critical (DI)**: `ISessionManager` resolved lazily via `IServiceProvider`
- **Critical (Auth)**: `GetUserId()` now checks `Jellyfin-UserId` claim first
- **Critical (Build)**: `SessionMessageType.Message` replaced by `SendMessageCommand`
- Client auth: `tryInit()` waits for `getCurrentUserId()` before first poll
- 204 No Content: proper handling for POST responses
- First poll: delayed 2s to avoid 401 at startup

### Added
- `[JsonPropertyName]` on `SendNotificationRequest` for deterministic binding
- CLAUDE.md developer guide

### Security
- Input validation: title max 120, message max 2000, TargetUserId = GUID or "All"
- Thread-safety: `volatile` + `Interlocked.CompareExchange` for lazy `ISessionManager`
- Connection string: `SqliteConnectionStringBuilder` instead of interpolation
- XSS: single-quote escaping in `esc()`
- Script cache: `Lazy<byte[]>` static for client script
- Error truncation: server responses capped at 200 chars before display

## [2.1.0] — 2026-05-25

### Added
- Migration from JSON to SQLite (WAL mode)
- Outbox pattern: persist, push, mark sent
- WebSocket push via `ISessionManager.SendMessageCommand`
- Admin history with filters, sort, search
- Auto-inject script tag into `index.html`

## [2.0.0] — 2026-05-24

### Added
- Complete architecture refactoring (Controller/Service/Repository)
- REST API with `[Authorize]` and `[Authorize(Policy = "RequiresElevation")]`
- Embedded admin pages via `IHasWebPages`
- Client script with bell, panel, modal
- SPA navigation interception (`pushState` / `replaceState`)

## [1.0.0] — 2026-05-23

### Added
- Initial plugin version
- Local JSON storage (`notifications.json`)
- Basic Send/List endpoints
- Simple bell icon in header
