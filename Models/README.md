# Models

Ce dossier contient les définitions de données du plugin — entités, DTOs et requêtes.

---

## Fichier

### `NotificationModels.cs`

Fichier unique regroupant toutes les classes de données :

### Entités

#### `NotificationEntity`
Représentation complète d'une notification en base de données.

| Propriété | Type | Description |
|-----------|------|-------------|
| `Id` | `string` | GUID unique |
| `Title` | `string` | Titre (max 120 chars) |
| `Message` | `string` | Corps du message (max 2000 chars) |
| `Type` | `string` | `Info`, `Warning`, ou `Alert` |
| `TargetUserId` | `string` | GUID utilisateur ou `"All"` |
| `DateCreated` | `string` | ISO 8601 UTC |
| `IsSent` | `bool` | Push WebSocket effectué |
| `ReadByUsers` | `List<string>` | GUIDs des utilisateurs ayant lu |

### DTOs (Data Transfer Objects)

#### `NotificationDto`
Vue côté client d'une notification. Exposé par `GET /Notification/List`.

| Propriété | Type | Description |
|-----------|------|-------------|
| `Id` | `string` | GUID |
| `Title` | `string` | Titre |
| `Message` | `string` | Corps |
| `Type` | `string` | Type |
| `Date` | `string` | Date formatée locale |
| `IsRead` | `bool` | Déjà lu par l'utilisateur courant |

#### `NotificationAdminDto`
Vue enrichie pour l'historique admin. Exposé par `GET /Notification/Admin/History`.

Inclut en plus : `TargetUserId`, `IsSent`, `ReadCount`.

### Requêtes

#### `SendNotificationRequest`
Body du `POST /Notification/Send`.

| Propriété | JSON | Description |
|-----------|------|-------------|
| `Title` | `title` | Titre de la notification |
| `Message` | `message` | Corps du message |
| `TargetUserId` | `targetUserId` | `"All"` ou GUID |
| `Type` | `type` | `Info`, `Warning`, ou `Alert` |

---

## Conventions

- Tous les DTOs utilisent `[JsonPropertyName]` explicites pour un binding JSON déterministe
- Les entités sont des POCO simples (pas d'Entity Framework)
- La conversion Entity → DTO se fait dans le Controller
