using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Jellyfin_notification.Models;

namespace Jellyfin_notification.Data
{
    /// <summary>
    /// Couche d'accès aux données : toutes les opérations SQLite du plugin.
    /// Aucune logique métier ici — uniquement du CRUD async.
    ///
    /// Chaque méthode ouvre sa propre connexion (WAL = safe en concurrence).
    /// Enregistré en Singleton dans PluginServiceRegistrar.
    /// </summary>
    public class NotificationRepository
    {
        private readonly NotificationDbContext _db;
        private readonly ILogger<NotificationRepository> _logger;

        public NotificationRepository(
            NotificationDbContext db,
            ILogger<NotificationRepository> logger)
        {
            _db     = db;
            _logger = logger;
        }

        // ──────────────────────────────────────────────────────────────
        //  Écriture
        // ──────────────────────────────────────────────────────────────

        /// <summary>Insère une nouvelle notification (IsSent = 0).</summary>
        public async Task<NotificationEntity> InsertAsync(
            NotificationEntity entity,
            CancellationToken ct = default)
        {
            using var conn = _db.CreateConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO Notifications (Id, TargetUserId, Title, Message, Type, DateCreated, IsSent)
                VALUES ($id, $target, $title, $msg, $type, $date, 0)
                """;
            cmd.Parameters.AddWithValue("$id",     entity.Id.ToString());
            cmd.Parameters.AddWithValue("$target", entity.TargetUserId);
            cmd.Parameters.AddWithValue("$title",  entity.Title);
            cmd.Parameters.AddWithValue("$msg",    entity.Message);
            cmd.Parameters.AddWithValue("$type",   entity.Type);
            cmd.Parameters.AddWithValue("$date",   entity.DateCreated.ToString("o"));

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            _logger.LogDebug("[JellyNotif] INSERT Notification {Id}.", entity.Id);
            return entity;
        }

        /// <summary>Marque une notification comme envoyée via WebSocket.</summary>
        public async Task MarkAsSentAsync(Guid id, CancellationToken ct = default)
        {
            using var conn = _db.CreateConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Notifications SET IsSent = 1 WHERE Id = $id";
            cmd.Parameters.AddWithValue("$id", id.ToString());
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Marque une notification comme lue par un utilisateur.
        /// Retourne <c>false</c> si la notification est introuvable.
        /// INSERT OR IGNORE = idempotent (double-clic sans erreur).
        /// </summary>
        public async Task<bool> MarkAsReadAsync(
            Guid notifId,
            string userId,
            CancellationToken ct = default)
        {
            using var conn = _db.CreateConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);

            // Vérifier existence
            using (var check = conn.CreateCommand())
            {
                check.CommandText = "SELECT 1 FROM Notifications WHERE Id = $id";
                check.Parameters.AddWithValue("$id", notifId.ToString());
                var exists = await check.ExecuteScalarAsync(ct).ConfigureAwait(false);
                if (exists is null) return false;
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT OR IGNORE INTO ReadStatus (NotificationId, UserId, ReadAt)
                VALUES ($notifId, $userId, $readAt)
                """;
            cmd.Parameters.AddWithValue("$notifId", notifId.ToString());
            cmd.Parameters.AddWithValue("$userId",  userId);
            cmd.Parameters.AddWithValue("$readAt",  DateTime.UtcNow.ToString("o"));
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            return true;
        }

        // ──────────────────────────────────────────────────────────────
        //  Lecture
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Retourne les 50 dernières notifications visibles par un utilisateur
        /// (globales "All" + personnelles), avec état isRead calculé en SQL.
        /// </summary>
        public async Task<List<NotificationDto>> GetForUserAsync(
            string userId,
            CancellationToken ct = default)
        {
            using var conn = _db.CreateConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT  n.Id,
                        n.Title,
                        n.Message,
                        n.Type,
                        n.DateCreated,
                        CASE WHEN rs.UserId IS NOT NULL THEN 1 ELSE 0 END AS IsRead
                FROM    Notifications n
                LEFT JOIN ReadStatus rs
                       ON rs.NotificationId = n.Id
                      AND rs.UserId = $userId
                WHERE   n.TargetUserId = 'All'
                   OR   n.TargetUserId = $userId
                ORDER BY n.DateCreated DESC
                LIMIT 50
                """;
            cmd.Parameters.AddWithValue("$userId", userId);

            var results = new List<NotificationDto>();
            var now     = DateTime.UtcNow;

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var date = DateTime.Parse(
                    reader.GetString(4),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind);

                results.Add(new NotificationDto
                {
                    Id      = reader.GetString(0),
                    Title   = reader.GetString(1),
                    Message = reader.GetString(2),
                    Type    = reader.GetString(3),
                    Date    = FormatRelativeDate(date, now),
                    IsRead  = reader.GetInt32(5) == 1
                });
            }

            return results;
        }

        /// <summary>Retourne toutes les notifications pour le tableau de bord admin.</summary>
        public async Task<List<AdminNotificationDto>> GetAllAsync(CancellationToken ct = default)
        {
            using var conn = _db.CreateConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT  n.Id,
                        n.Title,
                        n.Message,
                        n.Type,
                        n.TargetUserId,
                        n.DateCreated,
                        n.IsSent,
                        COUNT(rs.UserId) AS ReadCount
                FROM    Notifications n
                LEFT JOIN ReadStatus rs ON rs.NotificationId = n.Id
                GROUP BY n.Id
                ORDER BY n.DateCreated DESC
                """;

            var results = new List<AdminNotificationDto>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                results.Add(new AdminNotificationDto
                {
                    Id           = reader.GetString(0),
                    Title        = reader.GetString(1),
                    Message      = reader.GetString(2),
                    Type         = reader.GetString(3),
                    TargetUserId = reader.GetString(4),
                    DateCreated  = reader.GetString(5),
                    IsSent       = reader.GetInt32(6) == 1,
                    ReadCount    = reader.GetInt32(7)
                });
            }

            return results;
        }

        // ──────────────────────────────────────────────────────────────
        //  Maintenance
        // ──────────────────────────────────────────────────────────────

        /// <summary>Supprime les notifications antérieures à <paramref name="retentionDays"/> jours.</summary>
        public async Task PurgeOldAsync(int retentionDays, CancellationToken ct = default)
        {
            if (retentionDays <= 0) return;

            var cutoff = DateTime.UtcNow.AddDays(-retentionDays).ToString("o");

            using var conn = _db.CreateConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);

            using var cmd = conn.CreateCommand();
            // Supprime d'abord les ReadStatus orphelins (FK simulée)
            cmd.CommandText = """
                DELETE FROM ReadStatus
                WHERE NotificationId IN (
                    SELECT Id FROM Notifications WHERE DateCreated < $cutoff
                );
                DELETE FROM Notifications WHERE DateCreated < $cutoff;
                """;
            cmd.Parameters.AddWithValue("$cutoff", cutoff);
            var deleted = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

            if (deleted > 0)
                _logger.LogInformation("[JellyNotif] Purge : {N} entrée(s) supprimée(s).", deleted);
        }

        // ──────────────────────────────────────────────────────────────
        //  Helpers
        // ──────────────────────────────────────────────────────────────

        private static string FormatRelativeDate(DateTime date, DateTime now)
        {
            var diff = now - date;
            if (diff.TotalSeconds < 60) return "À l'instant";
            if (diff.TotalMinutes < 60) return $"Il y a {(int)diff.TotalMinutes} min";
            if (diff.TotalHours   < 24) return $"Il y a {(int)diff.TotalHours} h";
            if (diff.TotalDays    < 7)  return $"Il y a {(int)diff.TotalDays} j";
            return date.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
        }
    }
}
