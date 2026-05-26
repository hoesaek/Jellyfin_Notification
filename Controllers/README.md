# Controllers

Ce dossier contient les contrôleurs API REST du plugin, exposés automatiquement par le système de routing ASP.NET Core de Jellyfin.

---

## Fichiers

### `NotificationController.cs`

Point d'entrée HTTP principal du plugin. Expose les endpoints suivants :

| Endpoint | Méthode | Auth | Description |
|----------|---------|------|-------------|
| `/Notification/Send` | POST | Admin | Crée et pousse une notification |
| `/Notification/List` | GET | User | Retourne les notifications de l'utilisateur courant |
| `/Notification/MarkAsRead/{id}` | POST | User | Marque une notification comme lue |
| `/Notification/Admin/History` | GET | Admin | Historique complet pour le dashboard |

**Points techniques :**
- L'authentification s'appuie sur les claims Jellyfin (`Jellyfin-UserId` en priorité, avec fallback `NameIdentifier` / `sub`)
- La validation des inputs est stricte : titre ≤ 120 chars, message ≤ 2000 chars, TargetUserId = `All` ou GUID valide
- Le controller ne contient **aucune logique métier** — tout est délégué à `NotificationService`

### `ClientScriptController.cs`

Sert le fichier JavaScript client (`notif-client.js`) depuis les ressources embarquées de la DLL.

| Endpoint | Méthode | Auth | Description |
|----------|---------|------|-------------|
| `/JellyNotif/client` | GET | Public | Script JS client (cache 1h) |

**Points techniques :**
- Le script est mis en cache dans un `Lazy<byte[]>` statique (une seule lecture via reflection au premier appel)
- `ResponseCache(Duration = 3600)` côté HTTP pour éviter les requêtes répétées
- `AllowAnonymous` car le script est chargé avant toute authentification utilisateur

---

## Conventions

- Héritent de `ControllerBase` (pas `Controller` — pas de Views MVC)
- Route de base : `[Route("")]` → routes définies par attribut sur chaque méthode
- `[Authorize(Policy = "RequiresElevation")]` = admin uniquement
- `ConfigureAwait(false)` sur tous les `await`
