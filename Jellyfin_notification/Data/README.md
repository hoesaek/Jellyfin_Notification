# Data

Ce dossier contient la couche de persistance SQLite du plugin.

---

## Fichiers

### `NotificationDbContext.cs`

Gestionnaire de schéma et factory de connexions SQLite.

**Responsabilités :**
- Créer la base de données et la table `Notifications` au premier lancement
- Activer le mode WAL (Write-Ahead Logging) pour les performances en lecture concurrente
- Fournir des connexions SQLite via `CreateConnection()`
- Construire la connection string de manière sécurisée via `SqliteConnectionStringBuilder`

**Schéma de la table `Notifications` :**

| Colonne | Type | Description |
|---------|------|-------------|
| `Id` | TEXT (PK) | GUID unique |
| `Title` | TEXT NOT NULL | Titre de la notification |
| `Message` | TEXT NOT NULL | Corps du message |
| `Type` | TEXT NOT NULL | `Info`, `Warning`, `Alert` |
| `TargetUserId` | TEXT NOT NULL | GUID utilisateur ou `"All"` |
| `DateCreated` | TEXT NOT NULL | ISO 8601 UTC |
| `IsSent` | INTEGER | 0 ou 1 (push WebSocket effectué) |
| `ReadByUsers` | TEXT | JSON array des GUIDs ayant lu |

### `NotificationRepository.cs`

Couche d'accès aux données (DAO pattern).

**Méthodes :**

| Méthode | Description |
|---------|-------------|
| `InsertAsync()` | INSERT avec tous les champs paramétrés |
| `GetForUserAsync()` | SELECT pour un userId ou `"All"`, triées par date DESC |
| `MarkAsReadAsync()` | Ajoute le userId au JSON `ReadByUsers` |
| `MarkAsSentAsync()` | SET `IsSent = 1` |
| `GetAllAsync()` | SELECT * pour l'historique admin |
| `PurgeOldAsync()` | DELETE les notifications plus vieilles que N jours |

**Sécurité :**
- 100% des requêtes SQL utilisent des paramètres (`$param`) — aucune concaténation
- Toutes les méthodes sont `async` avec `CancellationToken`
- Chaque méthode ouvre et ferme sa propre connexion (pas de connexion partagée)

---

## Configuration

Le fichier de base est créé dans :
```
{PluginsConfigDir}/JellyfinNotification.db
```

Le chemin est résolu par `Plugin.cs` via `IApplicationPaths.PluginConfigurationsPath`.
