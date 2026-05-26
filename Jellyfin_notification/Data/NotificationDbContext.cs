using System;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Jellyfin_notification.Data
{
    /// <summary>
    /// Initialise et fournit les connexions SQLite pour le plugin.
    ///
    /// Responsabilités :
    ///   - Créer la base dans PluginConfigurationsPath au premier démarrage.
    ///   - Appliquer le schéma (CREATE TABLE IF NOT EXISTS).
    ///   - Fournir une connexion ouvrable à la demande (une par opération — thread-safe).
    ///
    /// Enregistré en Singleton dans PluginServiceRegistrar.
    /// </summary>
    public sealed class NotificationDbContext : IDisposable
    {
        private readonly string _connectionString;
        private readonly ILogger<NotificationDbContext> _logger;

        /// <param name="dbPath">Chemin complet vers le fichier .db (fourni par NotificationRepository via IApplicationPaths).</param>
        public NotificationDbContext(string dbPath, ILogger<NotificationDbContext> logger)
        {
            _logger           = logger;
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Cache = SqliteCacheMode.Shared
            };
            _connectionString = builder.ConnectionString;
            EnsureSchema();
        }

        /// <summary>Crée et retourne une connexion SQLite (à ouvrir puis disposer par l'appelant).</summary>
        public SqliteConnection CreateConnection() => new(_connectionString);

        // ──────────────────────────────────────────────────────────────
        //  Schéma
        // ──────────────────────────────────────────────────────────────

        private void EnsureSchema()
        {
            try
            {
                using var conn = CreateConnection();
                conn.Open();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    PRAGMA journal_mode=WAL;

                    CREATE TABLE IF NOT EXISTS Notifications (
                        Id           TEXT PRIMARY KEY NOT NULL,
                        TargetUserId TEXT NOT NULL DEFAULT 'All',
                        Title        TEXT NOT NULL,
                        Message      TEXT NOT NULL,
                        Type         TEXT NOT NULL DEFAULT 'Info',
                        DateCreated  TEXT NOT NULL,
                        IsSent       INTEGER NOT NULL DEFAULT 0
                    );

                    CREATE TABLE IF NOT EXISTS ReadStatus (
                        NotificationId TEXT NOT NULL,
                        UserId         TEXT NOT NULL,
                        ReadAt         TEXT NOT NULL,
                        PRIMARY KEY (NotificationId, UserId)
                    );

                    CREATE INDEX IF NOT EXISTS IX_Notifs_Target  ON Notifications(TargetUserId);
                    CREATE INDEX IF NOT EXISTS IX_Notifs_Date    ON Notifications(DateCreated);
                    CREATE INDEX IF NOT EXISTS IX_Read_UserId    ON ReadStatus(UserId);
                    """;
                cmd.ExecuteNonQuery();

                _logger.LogInformation("[JellyNotif] Schéma SQLite OK ({Path}).", _connectionString);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[JellyNotif] Échec init schéma SQLite.");
                throw; // fatal — le service ne doit pas démarrer sans DB
            }
        }

        /// <inheritdoc/>
        public void Dispose() { /* connexions gérées par opération */ }
    }
}
