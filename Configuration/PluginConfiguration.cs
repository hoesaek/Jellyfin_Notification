using MediaBrowser.Model.Plugins;

namespace Jellyfin_notification.Configuration
{
    /// <summary>
    /// Configuration du plugin (visible depuis le dashboard Jellyfin si besoin).
    /// Pour l'instant minimal — peut être étendu pour ajouter des options (max notifs, rétention, etc.).
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Nombre maximum de notifications conservées en base.
        /// Au-delà, les plus anciennes sont supprimées automatiquement.
        /// </summary>
        public int MaxNotifications { get; set; } = 200;

        /// <summary>
        /// Nombre de jours après lequel une notification est auto-supprimée (0 = désactivé).
        /// </summary>
        public int RetentionDays { get; set; } = 30;
    }
}
