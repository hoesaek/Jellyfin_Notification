# CLAUDE.md — Jellyfin Notification Plugin

Guide de référence pour les interventions IA sur ce repository.

---

## Commandes de build et déploiement

```bash
# Build standard (Release, net9.0)
dotnet build -c Release

# Déploiement complet (build + stop Jellyfin + copie DLL + injection index.html)
# Exécuter en administrateur (PowerShell)
.\deploy.ps1 -Version "X.X.X.X"

# Build uniquement, sans déploiement
dotnet build -c Release --no-restore
```

> Le `deploy.ps1` exécute dans l'ordre :
> 1. `dotnet build -c Release`
> 2. Arrêt du service Jellyfin
> 3. Suppression des anciennes versions dans `C:\ProgramData\Jellyfin\Server\plugins\`
> 4. Copie de la DLL + meta.json vers `Jellyfin_notification_{version}`
> 5. Injection du `<script>` dans `index.html` du web client

---

## Architecture du projet

```
Jellyfin_notification/
├── Plugin.cs                          # Point d'entrée — injection script, IHasWebPages
├── PluginServiceRegistrar.cs          # Enregistrement DI (Singleton pour tous les services)
│
├── Controllers/
│   ├── NotificationController.cs      # API REST : Send, List, MarkAsRead, Admin/History
│   └── ClientScriptController.cs      # Sert le JS embarqué (/JellyNotif/client)
│
├── Services/
│   └── NotificationService.cs         # Logique métier — Pattern Outbox (persist → push → mark)
│
├── Data/
│   ├── NotificationDbContext.cs        # Init schéma SQLite + factory de connexions
│   └── NotificationRepository.cs      # CRUD SQLite async (INSERT, SELECT, UPDATE, DELETE)
│
├── Models/
│   └── NotificationModels.cs          # NotificationEntity, DTOs (NotificationDto, AdminNotificationDto),
│                                      # SendNotificationRequest
│
├── Configuration/
│   └── PluginConfiguration.cs         # MaxNotifications, RetentionDays
│
├── ClientScript/
│   └── notif-client.js                # SPA client — cloche, panneau, modale, polling
│
├── Pages/
│   ├── send.html                      # Page admin : formulaire d'envoi
│   └── history.html                   # Page admin : historique
│
├── deploy.ps1                         # Script de déploiement automatisé
├── meta.json                          # Métadonnées NuGet/Jellyfin du plugin
└── Jellyfin_notification.csproj         # Projet .NET 9 — bibliothèque de classes
```

### Flux de données

```
Admin (send.html)
    │
    ▼
POST /Notification/Send  ──→  NotificationController
    │                              │
    │                              ▼
    │                         NotificationService.SendAsync()
    │                              │
    │                    ┌─────────┴─────────┐
    │                    ▼                   ▼
    │            (B) INSERT SQLite    (C) Push WebSocket
    │             via Repository      via ISessionManager
    │                    │                   │
    │                    ▼                   ▼
    │            (D) UPDATE IsSent    Toast natif Jellyfin
    │                                        │
    ▼                                        ▼
Client (notif-client.js)              Affiché immédiatement
    │
    ▼
GET /Notification/List  ←── Polling 60s (fallback Outbox)
    │
    ▼
Panneau cloche
```

---

## Responsabilités des fichiers clés

| Fichier | Responsabilité | Ne doit PAS contenir |
|---------|---------------|---------------------|
| `Plugin.cs` | Bootstrap, injection `<script>`, pages admin | Logique métier, accès DB |
| `NotificationController.cs` | Validation HTTP, routing, extraction claims | Logique métier, SQL |
| `NotificationService.cs` | Outbox (persist → push → mark), purge | SQL direct, accès HTTP |
| `NotificationRepository.cs` | CRUD SQLite pur, requêtes paramétrées | Logique métier, WebSocket |
| `NotificationDbContext.cs` | Schéma, connexion factory | Requêtes, logique |
| `notif-client.js` | UI cloche/panneau/modale, polling API | Logique serveur, SQL |
| `send.html` | Formulaire admin, appels fetch | Accès DB, modifications DOM globales |

---

## Conventions de code

### C# — Backend

- **Namespace** : `Jellyfin_notification.{Folder}` (ex: `Jellyfin_notification.Services`)
- **Logging** : Toujours préfixer par `[JellyNotif]` — ex: `_logger.LogInformation("[JellyNotif] ...")`
- **Async** : Toutes les méthodes I/O doivent être `async Task<T>` avec `CancellationToken ct` en dernier paramètre
- **ConfigureAwait** : Toujours `.ConfigureAwait(false)` sur les `await` (pas de contexte de synchronisation)
- **SQL** : Requêtes paramétrées uniquement (`$param`). JAMAIS de concaténation de strings dans les requêtes
- **DI** : Les services sont Singleton. Si une dépendance Jellyfin n'est pas disponible au démarrage, utiliser la résolution paresseuse via `IServiceProvider`
- **DTOs** : Toujours décorer avec `[JsonPropertyName("camelCase")]` pour un contrat API explicite
- **XML Docs** : Obligatoire sur les méthodes et classes publiques
- **Nullability** : `<Nullable>enable</Nullable>` — utiliser `?` explicitement, vérifier les null
- **Versioning** : Synchroniser `Version` dans `.csproj` ET `meta.json`

### JavaScript — Frontend

- **IIFE** : Tout le code dans `(function () { 'use strict'; ... })();`
- **Préfixe console** : `[JellyNotif]` sur tous les `console.log/warn/error`
- **Échappement XSS** : Utiliser `escHtml()` pour TOUTE donnée serveur insérée dans le DOM via `innerHTML`
- **textContent** : Préférer `textContent` à `innerHTML` quand aucun markup n'est nécessaire
- **ApiClient** : Utiliser `window.ApiClient.ajax()` avec des chemins relatifs (jamais d'URL absolues — le token est injecté automatiquement)
- **Authentification** : Toujours vérifier `getCurrentUserId()` avant d'appeler les endpoints
- **Polling** : `POLL_INTERVAL = 60_000` (60s). Ne pas descendre en dessous

---

## Règles de sécurité

### Authentification Jellyfin

- **Claim utilisateur** : Jellyfin 10.11.x utilise `"Jellyfin-UserId"` (claim personnalisé). Toujours chercher dans cet ordre :
  1. `User.FindFirst("Jellyfin-UserId")`
  2. `User.FindFirst(ClaimTypes.NameIdentifier)`
  3. `User.FindFirst("sub")`
- **Admin** : Les endpoints admin utilisent `[Authorize(Policy = "RequiresElevation")]` (natif Jellyfin)
- **User** : Les endpoints utilisateur utilisent `[Authorize]` + extraction du claim UserId
- **Script client** : `[AllowAnonymous]` car le `<script>` est chargé avant l'auth. Le script ne contient aucune donnée sensible

### Validation des données

- **Titre** : max 120 caractères, obligatoire, trimmed
- **Message** : max 2000 caractères, obligatoire, trimmed
- **TargetUserId** : doit être `"All"` ou un GUID valide (Guid.TryParse)
- **Type** : whitelist stricte `{ "Info", "Warning", "Alert" }` — tout autre valeur → fallback `"Info"`
- **notifId** : contraint par le route template `{notifId:guid}` — invalide = 404 automatique

### Prévention XSS

- **Backend** : Les réponses JSON sont échappées nativement par `System.Text.Json`
- **Frontend (notif-client.js)** : `escHtml()` échappe `& < > " '` avant tout `innerHTML`
- **Frontend (send.html)** : `showFeedback()` utilise `textContent` (pas d'innerHTML avec des données user)
- **Erreurs serveur** : Tronquées à 200 caractères avant affichage côté client

### SQLite

- **Paramètres** : Toujours `$param` dans les requêtes, jamais de concaténation
- **Connection string** : Construite via `SqliteConnectionStringBuilder` (pas d'interpolation — prévention injection)
- **WAL mode** : Activé au démarrage pour la concurrence lecture/écriture
- **Connexion par opération** : Chaque méthode du repository ouvre et dispose sa propre connexion

### Résolution DI paresseuse

`ISessionManager` n'est pas disponible dans le conteneur DI au moment de `RegisterServices()` (les services Jellyfin sont enregistrés après les plugins). Solution :
- Injecter `IServiceProvider` au lieu de `ISessionManager`
- Résoudre via une propriété thread-safe avec `Interlocked.CompareExchange`
- La résolution est amortie (une seule fois, puis cached)

---

## Fichiers à ne PAS modifier

| Fichier | Raison |
|---------|--------|
| `build.yaml` | CI/CD template, non utilisé actuellement |
| `index.html` (Jellyfin) | Modifié dynamiquement par `Plugin.cs` au démarrage |

---

## Erreurs courantes et solutions

| Symptôme | Cause | Solution |
|----------|-------|----------|
| 500 `Unable to resolve ISessionManager` | DI ordre de registration | Résolution paresseuse via IServiceProvider |
| 401 sur `GET /Notification/List` | Mauvais claim UserId | Chercher `"Jellyfin-UserId"` en priorité |
| `SessionMessageType.Message` compile error | Enum inexistant en 10.11.8 | Utiliser `SendMessageCommand` par session |
| Script client non chargé | `<script>` non injecté | Vérifier `deploy.ps1` étape 5, ou `Plugin.cs` |
| Premier poll en 401 | `tryInit()` sans vérifier l'auth | Exiger `getCurrentUserId()` non-null |

---

## Compatibilité

- **Jellyfin** : 10.11.x (targetAbi: `10.11.0.0`)
- **.NET** : 9.0
- **SQLite** : Fourni par le runtime Jellyfin (pas de DLL native à embarquer)
- **Navigateurs** : Tous les navigateurs supportés par le web client Jellyfin
