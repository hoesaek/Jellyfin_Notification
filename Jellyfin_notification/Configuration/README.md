# Configuration

Ce dossier contient les options de configuration du plugin.

---

## Fichier

### `PluginConfiguration.cs`

Configuration persistée automatiquement par Jellyfin dans le fichier XML du plugin.

| Option | Type | Défaut | Description |
|--------|------|--------|-------------|
| `MaxNotifications` | `int` | `200` | Nombre maximum de notifications conservées en base |
| `RetentionDays` | `int` | `30` | Suppression automatique des notifications plus vieilles que N jours (0 = désactivé) |

---

## Usage

La configuration est accessible dans le code via :

```csharp
var config = Plugin.Instance!.Configuration;
var max = config.MaxNotifications;
var days = config.RetentionDays;
```

Les valeurs sont éditables depuis le dashboard Jellyfin :
**Dashboard → Plugins → Jellyfin Notification → Configuration**

---

## Mécanisme Jellyfin

Jellyfin sérialise automatiquement la configuration en XML dans :
```
{PluginConfigurationsPath}/Jellyfin_notification.xml
```

La classe hérite de `BasePluginConfiguration` et n'a pas besoin de logique custom — Jellyfin gère la lecture/écriture et l'UI de configuration automatiquement.
