# Data

This folder contains the plugin's SQLite persistence layer.

---

## Files

### `NotificationDbContext.cs`

SQLite schema manager and connection factory.

**Responsibilities:**
- Create the database and `Notifications` table on first launch
- Enable WAL (Write-Ahead Logging) mode for concurrent read performance
- Provide SQLite connections via `CreateConnection()`
- Build the connection string securely via `SqliteConnectionStringBuilder`

**`Notifications` table schema:**

| Column | Type | Description |
|---------|------|-------------|
| `Id` | TEXT (PK) | Unique GUID |
| `Title` | TEXT NOT NULL | Notification title |
| `Message` | TEXT NOT NULL | Message body |
| `Type` | TEXT NOT NULL | `Info`, `Warning`, `Alert` |
| `TargetUserId` | TEXT NOT NULL | User GUID or `"All"` |
| `DateCreated` | TEXT NOT NULL | ISO 8601 UTC |
| `IsSent` | INTEGER | 0 or 1 (WebSocket push done) |
| `ReadByUsers` | TEXT | JSON array of GUIDs who have read |

### `NotificationRepository.cs`

Data access layer (DAO pattern).

**Methods:**

| Method | Description |
|---------|-------------|
| `InsertAsync()` | INSERT with all parameterized fields |
| `GetForUserAsync()` | SELECT for a userId or `"All"`, sorted by date DESC |
| `MarkAsReadAsync()` | Adds the userId to the `ReadByUsers` JSON |
| `MarkAsSentAsync()` | SET `IsSent = 1` |
| `GetAllAsync()` | SELECT * for admin history |
| `PurgeOldAsync()` | DELETE notifications older than N days |

**Security:**
- 100% of SQL queries use parameters (`$param`) — no concatenation
- All methods are `async` with `CancellationToken`
- Each method opens and closes its own connection (no shared connection)

---

## Configuration

The database file is created in:
```
{PluginsConfigDir}/JellyfinNotification.db
```

The path is resolved by `Plugin.cs` via `IApplicationPaths.PluginConfigurationsPath`.
