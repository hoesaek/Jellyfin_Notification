# Pages

This folder contains the admin HTML pages embedded in the plugin's DLL.

---

## Files

### `send.html`

Notification send form, accessible from the Jellyfin admin dashboard.

**URL**: `/web/#/configurationpage?name=JellyNotifSend`

**Features:**
- Recipient selector (all users or targeted)
- Type selector in colored pills (Info, Warning, Alert)
- Title and message fields with real-time character counters
- Client validation before sending (required, max length)
- Animated feedback (success/error)
- Button to history

**Security guard:**
The JavaScript checks admin rights via `GET /Users/{userId}` before displaying the user list. A non-admin sees an error message and the button is disabled.

### `history.html`

History table of sent notifications.

**URL**: `/web/#/configurationpage?name=JellyNotifHistory`

**Features:**
- Stats cards (Total, Info, Warning, Alert, Sent)
- Filtering by type, by recipient, text search
- Sort by column (title, recipient, type, date, sent, read count)
- Expandable row to see the full message
- Colored type badges
- Sent indicator (green/grey dot)

---

## Jellyfin Integration

The pages are declared as `EmbeddedResource` in the `.csproj` and exposed via `IHasWebPages` in `Plugin.cs`:

```csharp
public IEnumerable<PluginPageInfo> GetPages() =>
[
    new PluginPageInfo
    {
        Name             = "JellyNotifSend",
        EmbeddedResourcePath = "Jellyfin_notification.Pages.send.html",
        EnableInMainMenu = true,
        MenuSection      = "admin",
        DisplayName      = "Jellyfin Notification — Send"
    },
    // ...
];
```

They appear automatically in **Dashboard → Plugins → Jellyfin Notification**.

---

## JavaScript Conventions in admin pages

- Authentication via `localStorage.getItem('jellyfin_credentials')`
- No use of `ApiClient` (unavailable in admin pages)
- Direct `fetch()` with `MediaBrowser Token="..."` header
- `textContent` for user data (anti-XSS)
- `escHtml()` for any `innerHTML` with server data
