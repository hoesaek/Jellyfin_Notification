# Configuration

This folder contains the plugin's configuration options.

---

## File

### `PluginConfiguration.cs`

Configuration automatically persisted by Jellyfin in the plugin's XML file.

| Option | Type | Default | Description |
|--------|------|--------|-------------|
| `MaxNotifications` | `int` | `200` | Maximum number of notifications kept in database |
| `RetentionDays` | `int` | `30` | Automatic deletion of notifications older than N days (0 = disabled) |

---

## Usage

The configuration is accessible in the code via:

```csharp
var config = Plugin.Instance!.Configuration;
var max = config.MaxNotifications;
var days = config.RetentionDays;
```

Values are editable from the Jellyfin dashboard:
**Dashboard → Plugins → Jellyfin Notification → Configuration**

---

## Jellyfin Mechanism

Jellyfin automatically serializes the configuration to XML in:
```
{PluginConfigurationsPath}/Jellyfin_notification.xml
```

The class inherits from `BasePluginConfiguration` and does not need custom logic — Jellyfin automatically handles read/write and the configuration UI.
