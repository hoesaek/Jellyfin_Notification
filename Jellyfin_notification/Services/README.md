# Services

Ce dossier contient la couche métier du plugin — le moteur de notification.

---

## Fichiers

### `NotificationService.cs`

Service singleton enregistré via `PluginServiceRegistrar`. Implémente le **pattern Outbox** :

```
SendAsync()
  ├── (1) INSERT en base (persist)        → NotificationRepository
  ├── (2) Push WebSocket (toast natif)    → ISessionManager
  └── (3) UPDATE IsSent = true            → NotificationRepository
```

**Responsabilités :**
- Orchestrer le flux persist → push → mark sent
- Résoudre `ISessionManager` de manière paresseuse (indisponible au `RegisterServices()`)
- Cibler les sessions actives de l'utilisateur ou diffuser à tous (`"All"`)
- Déléguer tout accès SQLite au `NotificationRepository`

**Points techniques :**

| Concept | Implémentation |
|---------|---------------|
| Lazy init thread-safe | `volatile` + `Interlocked.CompareExchange` sur `_sessionManager` |
| Push WebSocket | `ISessionManager.SendMessageCommand()` par session |
| Ciblage | `"All"` → toutes les sessions actives / GUID → sessions de cet utilisateur |
| Logging | `ILogger<NotificationService>` avec préfixe `[JellyNotif]` |

**Ne doit PAS contenir :**
- Du SQL direct (→ Repository)
- De la validation HTTP (→ Controller)
- De la logique d'affichage (→ Client JS)

---

## Injection de dépendances

```csharp
services.AddSingleton<NotificationService>();
```

Le service est résolu une seule fois au démarrage. `ISessionManager` est résolu au premier accès (lazy) car le serveur Jellyfin ne l'a pas encore enregistré au moment du `RegisterServices()`.
