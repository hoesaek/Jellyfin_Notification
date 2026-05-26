# ClientScript

Ce dossier contient le script JavaScript client injecté automatiquement dans toutes les pages de l'interface Jellyfin.

---

## Fichier

### `notif-client.js`

Script IIFE (Immediately Invoked Function Expression) injecté dans `index.html` au démarrage de Jellyfin par `Plugin.cs`.

**Tag d'injection :**
```html
<script plugin="JellyfinNotification" version="2.3.0.0" src="/JellyNotif/client?v=2.3.0.0" defer></script>
```

---

## Composants UI

### 🔔 Cloche (Bell Button)
- Injectée dans `.headerRight` de la barre de navigation
- Badge rouge avec compteur des non-lus (animation scale spring)
- Sélecteurs robustes multi-version Jellyfin

### 📋 Panneau (Panel)
- Dropdown fixé top-right avec backdrop blur
- Header : titre + boutons "Tout lu" / "Tout effacer"
- Liste des notifications avec :
  - Accent bar colorée par type (bleu/jaune/rouge)
  - Titre, date relative, aperçu du message
  - Bouton ✕ pour supprimer (slide-out animation)
- Animation slide-in échelonnée à l'ouverture
- Fermeture : clic extérieur ou Escape

### 💬 Modale (Modal)
- Bannière de type colorée avec badge et date
- Icône emoji par type (ℹ️ / ⚠️ / 🚨)
- Corps du message en texte formaté
- Boutons : "🗑 Supprimer" (ghost) + "Fermer" (gradient)
- Backdrop avec blur + animation spring

### 🗑️ Système de suppression (Dismiss)
- Stockage dans `localStorage` (clé `JellyNotif_dismissed`)
- Maximum 200 IDs conservés (évite le bloat)
- Filtrage des notifications dismissed au refresh
- "Tout effacer" : marque tout lu + dismiss all

---

## Flux de données

```
tryInit() ← polling 500ms / événements Jellyfin
  │
  ├── window.ApiClient prêt + utilisateur authentifié ?
  │   └── init()
  │       ├── injectStyles()        → <style> dans <head>
  │       ├── buildUI()             → cloche + panneau + modale
  │       ├── loadDismissed()       → localStorage
  │       └── setInterval(refresh)  → polling 60s
  │
  └── refresh()
      ├── fetchNotifications()     → GET /Notification/List (via ApiClient.ajax)
      ├── filtrage dismissed       → localStorage
      └── renderNotifications()    → DOM update
```

---

## Gestion du SPA

Jellyfin est une SPA qui utilise `history.pushState()` pour naviguer. Le script intercepte :

```javascript
history.pushState    = function (...args) { _pushState(...args);    onSpaNav(); };
history.replaceState = function (...args) { _replaceState(...args); onSpaNav(); };
```

`onSpaNav()` re-vérifie et re-construit l'UI si le DOM a été réinitialisé par Jellyfin.

---

## API utilisée

| Appel | Méthode | Usage |
|-------|---------|-------|
| `/Notification/List` | GET | Récupérer les notifications de l'utilisateur |
| `/Notification/MarkAsRead/{id}` | POST | Marquer comme lue (retourne 204) |
| `/Users/{userId}` | GET | Vérifier les droits admin (sidebar) |

Tous les appels passent par `window.ApiClient.ajax()` qui gère automatiquement l'authentification.
