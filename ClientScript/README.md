# ClientScript

This folder contains the client JavaScript script automatically injected into all Jellyfin interface pages.

---

## File

### `notif-client.js`

IIFE (Immediately Invoked Function Expression) script injected into `index.html` at Jellyfin startup by `Plugin.cs`.

**Injection tag:**
```html
<script plugin="JellyfinNotification" version="2.3.0.0" src="/JellyNotif/client?v=2.3.0.0" defer></script>
```

---

## UI Components

### Bell Button
- Injected into `.headerRight` of the navigation bar
- Red badge with unread counter (spring scale animation)
- Robust selectors for multiple Jellyfin versions

### Panel
- Dropdown fixed top-right with backdrop blur
- Header: title + "Mark all read" / "Clear all" buttons
- Notifications list with:
  - Color accent bar by type (blue/yellow/red)
  - Title, relative date, message preview
  - ✕ button to delete (slide-out animation)
- Staggered slide-in animation on open
- Close: click outside or Escape

### Modal
- Colored type banner with badge and date
- Type icon (Info / Warning / Alert)
- Formatted text message body
- Buttons: "Delete" (ghost) + "Close" (gradient)
- Backdrop with blur + spring animation

### Dismiss System
- Stored in `localStorage` (`JellyNotif_dismissed` key)
- Maximum 200 IDs kept (prevents bloat)
- Filtering of dismissed notifications on refresh
- "Clear all": marks all read + dismiss all

---

## Data flow

```
tryInit() ← polling 500ms / Jellyfin events
  │
  ├── window.ApiClient ready + user authenticated ?
  │   └── init()
  │       ├── injectStyles()        → <style> in <head>
  │       ├── buildUI()             → bell + panel + modal
  │       ├── loadDismissed()       → localStorage
  │       └── setInterval(refresh)  → polling 60s
  │
  └── refresh()
      ├── fetchNotifications()     → GET /Notification/List (via ApiClient.ajax)
      ├── filtering dismissed      → localStorage
      └── renderNotifications()    → DOM update
```

---

## SPA Management

Jellyfin is a SPA that uses `history.pushState()` to navigate. The script intercepts:

```javascript
history.pushState    = function (...args) { _pushState(...args);    onSpaNav(); };
history.replaceState = function (...args) { _replaceState(...args); onSpaNav(); };
```

`onSpaNav()` re-checks and re-builds the UI if the DOM was reset by Jellyfin.

---

## Used API

| Call | Method | Usage |
|-------|---------|-------|
| `/Notification/List` | GET | Retrieve user's notifications |
| `/Notification/MarkAsRead/{id}` | POST | Mark as read (returns 204) |
| `/Users/{userId}` | GET | Check admin rights (sidebar) |

All calls go through `window.ApiClient.ajax()` which automatically handles authentication.
