# Pages

Ce dossier contient les pages HTML d'administration embarquées dans la DLL du plugin.

---

## Fichiers

### `send.html`

Formulaire d'envoi de notification, accessible depuis le dashboard admin Jellyfin.

**URL** : `/web/#/configurationpage?name=JellyNotifSend`

**Fonctionnalités :**
- Sélecteur de destinataire (tous les utilisateurs ou ciblé)
- Sélecteur de type en pills colorées (Info, Warning, Alert)
- Champs titre et message avec compteurs de caractères temps réel
- Validation client avant envoi (required, longueur max)
- Feedback animé (succès/erreur)
- Bouton vers l'historique

**Garde de sécurité :**
Le JavaScript vérifie les droits admin via `GET /Users/{userId}` avant d'afficher la liste des utilisateurs. Un non-admin voit un message d'erreur et le bouton est désactivé.

### `history.html`

Tableau d'historique des notifications envoyées.

**URL** : `/web/#/configurationpage?name=JellyNotifHistory`

**Fonctionnalités :**
- Stats cards (Total, Info, Warning, Alert, Envoyées)
- Filtrage par type, par destinataire, recherche textuelle
- Tri par colonne (titre, destinataire, type, date, sent, read count)
- Ligne expandable pour voir le message complet
- Type badges colorés
- Indicateur d'envoi (dot vert/gris)

---

## Intégration Jellyfin

Les pages sont déclarées comme `EmbeddedResource` dans le `.csproj` et exposées via `IHasWebPages` dans `Plugin.cs` :

```csharp
public IEnumerable<PluginPageInfo> GetPages() =>
[
    new PluginPageInfo
    {
        Name             = "JellyNotifSend",
        EmbeddedResourcePath = "Jellyfin_notification.Pages.send.html",
        EnableInMainMenu = true,
        MenuSection      = "admin",
        DisplayName      = "Jellyfin Notification — Envoyer"
    },
    // ...
];
```

Elles apparaissent automatiquement dans **Dashboard → Plugins → Jellyfin Notification**.

---

## Conventions JavaScript dans les pages admin

- Authentification via `localStorage.getItem('jellyfin_credentials')`
- Pas d'utilisation de `ApiClient` (indisponible dans les pages admin)
- `fetch()` direct avec header `MediaBrowser Token="..."`
- `textContent` pour les données utilisateur (anti-XSS)
- `escHtml()` pour tout `innerHTML` avec données serveur
