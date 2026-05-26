using System.IO;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Jellyfin_notification.Data;
using Jellyfin_notification.Services;

namespace Jellyfin_notification
{
    /// <summary>
    /// Enregistre les services du plugin dans le conteneur DI de Jellyfin.
    ///
    /// Ordre d'enregistrement :
    ///   1. NotificationDbContext (Singleton) — init schéma SQLite au démarrage
    ///   2. NotificationRepository (Singleton) — accès data
    ///   3. NotificationService (Singleton)    — logique métier + Outbox
    ///
    /// Note : ILogger&lt;T&gt;, ISessionManager et IApplicationPaths sont déjà
    /// disponibles dans le conteneur Jellyfin, on les injecte simplement.
    /// </summary>
    public class PluginServiceRegistrar : IPluginServiceRegistrator
    {
        /// <inheritdoc/>
        public void RegisterServices(
            IServiceCollection serviceCollection,
            IServerApplicationHost applicationHost)
        {
            // Résoudre le chemin DB via IApplicationPaths (injecté par Jellyfin)
            serviceCollection.AddSingleton(sp =>
            {
                var appPaths = sp.GetRequiredService<IApplicationPaths>();
                var dbDir    = appPaths.PluginConfigurationsPath;
                Directory.CreateDirectory(dbDir);
                var dbPath = Path.Combine(dbDir, "JellyfinNotification.db");

                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<NotificationDbContext>>();
                return new NotificationDbContext(dbPath, logger);
            });

            serviceCollection.AddSingleton<NotificationRepository>();
            serviceCollection.AddSingleton<NotificationService>();
        }
    }
}
