using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;
using Jellyfin_notification.Configuration;

namespace Jellyfin_notification
{
    /// <summary>
    /// Classe principale du plugin Jellyfin Notification.
    ///
    /// Responsabilités :
    ///   - Au démarrage : injecte un &lt;script&gt; dans index.html
    ///   - À la désinstallation : retire le &lt;script&gt; proprement
    ///   - Implémente IHasWebPages pour servir les pages d'admin embarquées
    ///
    /// Toute la logique métier (notifications, DB) est dans NotificationService.
    /// </summary>
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        private const string PluginTag = "JellyfinNotification";

        private readonly ILogger<Plugin> _logger;
        private readonly IApplicationPaths _appPaths;

        /// <summary>Singleton accessible depuis les services (pour la config).</summary>
        public static Plugin? Instance { get; private set; }

        /// <inheritdoc/>
        public override string Name => "Jellyfin Notification";

        /// <inheritdoc/>
        public override Guid Id => new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

        /// <inheritdoc/>
        public override string Description =>
            "Notifications personnalisées : envoi ciblé avec push WebSocket, cloche native, historique admin.";

        /// <summary>
        /// Chemin vers le index.html du web client Jellyfin.
        /// Plusieurs fallbacks pour couvrir les installations service vs tray.
        /// </summary>
        private string IndexHtmlPath
        {
            get
            {
                if (!string.IsNullOrEmpty(_appPaths.WebPath))
                    return Path.Combine(_appPaths.WebPath, "index.html");

                var candidate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "jellyfin-web", "index.html");
                if (File.Exists(candidate))
                    return candidate;

                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Jellyfin", "Server", "jellyfin-web", "index.html");
            }
        }

        /// <summary>
        /// Constructeur résolu par le DI Jellyfin.
        /// ILogger&lt;Plugin&gt; est disponible nativement — plus besoin du wrapper Logger.
        /// </summary>
        public Plugin(
            IApplicationPaths applicationPaths,
            IXmlSerializer xmlSerializer,
            ILogger<Plugin> logger)
            : base(applicationPaths, xmlSerializer)
        {
            Instance  = this;
            _appPaths = applicationPaths;
            _logger   = logger;

            _logger.LogInformation("[JellyNotif] Plugin v{Version} démarré.", Version);

            CleanupOldScript();
            UpdateIndexHtml(inject: true);
        }

        /// <inheritdoc/>
        public override void OnUninstalling()
        {
            UpdateIndexHtml(inject: false);
            base.OnUninstalling();
        }

        // ──────────────────────────────────────────────────────────────
        //  Gestion index.html
        // ──────────────────────────────────────────────────────────────

        private void CleanupOldScript()
        {
            try
            {
                if (!File.Exists(IndexHtmlPath))
                {
                    _logger.LogError("[JellyNotif] index.html introuvable : {Path}", IndexHtmlPath);
                    return;
                }

                var content = File.ReadAllText(IndexHtmlPath);
                var regex = new Regex(
                    $@"<script[^>]*plugin=[""']{PluginTag}[""'][^>]*>\s*</script>\n?",
                    RegexOptions.IgnoreCase);

                if (regex.IsMatch(content))
                {
                    _logger.LogInformation("[JellyNotif] Ancienne balise script détectée — nettoyage.");
                    File.WriteAllText(IndexHtmlPath, regex.Replace(content, string.Empty));
                    _logger.LogInformation("[JellyNotif] Nettoyage terminé.");
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "[JellyNotif] Accès refusé à index.html (CleanupOldScript).");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[JellyNotif] Erreur CleanupOldScript.");
            }
        }

        private void UpdateIndexHtml(bool inject)
        {
            try
            {
                if (!File.Exists(IndexHtmlPath))
                {
                    _logger.LogError("[JellyNotif] index.html introuvable : {Path}", IndexHtmlPath);
                    return;
                }

                var content = File.ReadAllText(IndexHtmlPath);

                // Retirer toute ancienne version (évite les doublons)
                var regex = new Regex(
                    $@"<script[^>]*plugin=[""']{PluginTag}[""'][^>]*>\s*</script>\n?",
                    RegexOptions.IgnoreCase);
                content = regex.Replace(content, string.Empty);

                if (inject)
                {
                    var scriptUrl = $"/JellyNotif/client?v={Version}";
                    var scriptTag = $@"<script plugin=""{PluginTag}"" version=""{Version}"" src=""{scriptUrl}"" defer></script>";

                    const string closingBody = "</body>";
                    const string closingHtml = "</html>";

                    if (content.Contains(closingBody, StringComparison.OrdinalIgnoreCase))
                    {
                        content = content.Replace(closingBody, $"{scriptTag}\n{closingBody}", StringComparison.OrdinalIgnoreCase);
                        File.WriteAllText(IndexHtmlPath, content);
                        _logger.LogInformation("[JellyNotif] Script injecté dans index.html (URL={Url}).", scriptUrl);
                    }
                    else if (content.Contains(closingHtml, StringComparison.OrdinalIgnoreCase))
                    {
                        content = content.Replace(closingHtml, $"{scriptTag}\n{closingHtml}", StringComparison.OrdinalIgnoreCase);
                        File.WriteAllText(IndexHtmlPath, content);
                        _logger.LogInformation("[JellyNotif] </body> absent — script injecté avant </html>.");
                    }
                    else
                    {
                        _logger.LogError("[JellyNotif] Ni </body> ni </html> trouvé — script non injecté.");
                    }
                }
                else
                {
                    File.WriteAllText(IndexHtmlPath, content);
                    _logger.LogInformation("[JellyNotif] Script retiré de index.html (désinstallation).");
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex,
                    "[JellyNotif] Accès refusé à index.html — vérifier permissions NetworkService sur {Path}.",
                    IndexHtmlPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[JellyNotif] Erreur UpdateIndexHtml(inject={Inject}).", inject);
            }
        }

        // ──────────────────────────────────────────────────────────────
        //  Pages d'administration (IHasWebPages)
        // ──────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public IEnumerable<PluginPageInfo> GetPages()
        {
            const string ns = "Jellyfin_notification";
            return new[]
            {
                new PluginPageInfo
                {
                    Name                 = "JellyNotifSend",
                    DisplayName          = "Notifications — Envoyer",
                    EmbeddedResourcePath = $"{ns}.Pages.send.html",
                    EnableInMainMenu     = true
                },
                new PluginPageInfo
                {
                    Name                 = "JellyNotifHistory",
                    DisplayName          = "Notifications — Historique",
                    EmbeddedResourcePath = $"{ns}.Pages.history.html",
                    EnableInMainMenu     = false
                }
            };
        }
    }
}
