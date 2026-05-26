# Models

This folder contains the plugin's data definitions — entities, DTOs, and requests.

---

## File

### `NotificationModels.cs`

Single file grouping all data classes:

### Entities

#### `NotificationEntity`
Complete representation of a notification in the database.

| Property | Type | Description |
|-----------|------|-------------|
| `Id` | `string` | Unique GUID |
| `Title` | `string` | Title (max 120 chars) |
| `Message` | `string` | Message body (max 2000 chars) |
| `Type` | `string` | `Info`, `Warning`, or `Alert` |
| `TargetUserId` | `string` | User GUID or `"All"` |
| `DateCreated` | `string` | ISO 8601 UTC |
| `IsSent` | `bool` | WebSocket push done |
| `ReadByUsers` | `List<string>` | GUIDs of users who have read |

### DTOs (Data Transfer Objects)

#### `NotificationDto`
Client-side view of a notification. Exposed by `GET /Notification/List`.

| Property | Type | Description |
|-----------|------|-------------|
| `Id` | `string` | GUID |
| `Title` | `string` | Title |
| `Message` | `string` | Body |
| `Type` | `string` | Type |
| `Date` | `string` | Local formatted date |
| `IsRead` | `bool` | Already read by the current user |

#### `NotificationAdminDto`
Enriched view for admin history. Exposed by `GET /Notification/Admin/History`.

Also includes: `TargetUserId`, `IsSent`, `ReadCount`.

### Requests

#### `SendNotificationRequest`
Body of `POST /Notification/Send`.

| Property | JSON | Description |
|-----------|------|-------------|
| `Title` | `title` | Notification title |
| `Message` | `message` | Message body |
| `TargetUserId` | `targetUserId` | `"All"` or GUID |
| `Type` | `type` | `Info`, `Warning`, or `Alert` |

---

## Conventions

- All DTOs use explicit `[JsonPropertyName]` for deterministic JSON binding
- Entities are simple POCOs (no Entity Framework)
- Entity → DTO conversion is done in the Controller
