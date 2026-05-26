using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Jellyfin_notification.Configuration;
using Jellyfin_notification.Data;
using Jellyfin_notification.Models;

namespace Jellyfin_notification.Services
{
    /// <summary>
    /// Service central — Pattern Outbox :
    ///
    ///   (A) Admin POST /Send
    ///   (B) INSERT en SQLite           (IsSent = 0)
    ///   (C) Push via ISessionManager   (WebSocket natif Jellyfin)
    ///   (D) UPDATE IsSent = 1          (si push OK)
    ///
    /// Le GET /Notification/List est le fallback pour les clients offline
    /// au moment du push.
    ///
    /// Enregistré en Singleton dans PluginServiceRegistrar.
    ///
    /// NOTE : ISessionManager est résolu de manière paresseuse via IServiceProvider
    /// car il n'est pas encore enregistré au moment où RegisterServices() s'exécute.
    /// </summary>
    public class NotificationService
    {
        private static readonly HashSet<string> ValidTypes =
            new(StringComparer.OrdinalIgnoreCase) { "Info", "Warning", "Alert" };

        private readonly NotificationRepository _repo;
        private readonly IServiceProvider       _serviceProvider;
        private readonly ILogger<NotificationService> _logger;

        // Résolution paresseuse : ISessionManager n'est disponible qu'après
        // l'initialisation complète du serveur Jellyfin.
        private volatile ISessionManager? _sessionManager;
        private ISessionManager SessionManager
        {
            get
            {
                // Double-checked locking pour thread-safety (Singleton service)
                if (_sessionManager is null)
                {
                    var resolved = _serviceProvider.GetRequiredService<ISessionManager>();
                    Interlocked.CompareExchange(ref _sessionManager, resolved, null);
                }
                return _sessionManager;
            }
        }

        public NotificationService(
            NotificationRepository repo,
            IServiceProvider serviceProvider,
            ILogger<NotificationService> logger)
        {
            _repo            = repo;
            _serviceProvider = serviceProvider;
            _logger          = logger;
        }

        // ──────────────────────────────────────────────────────────────
        //  (A)+(B)+(C)+(D) — Envoi complet (Outbox)
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Crée, persiste et envoie immédiatement une notification.
        /// Retourne l'entité insérée.
        /// </summary>
        public async Task<NotificationEntity> SendAsync(
            SendNotificationRequest req,
            CancellationToken ct = default)
        {
            // Validation / normalisation
            var entity = new NotificationEntity
            {
                Title        = req.Title.Trim(),
                Message      = req.Message.Trim(),
                TargetUserId = string.IsNullOrWhiteSpace(req.TargetUserId) ? "All" : req.TargetUserId.Trim(),
                Type         = ValidTypes.Contains(req.Type) ? req.Type : "Info",
                DateCreated  = DateTime.UtcNow
            };

            // (B) Persister en SQLite AVANT d'essayer le push (durabilité)
            await _repo.InsertAsync(entity, ct).ConfigureAwait(false);
            _logger.LogInformation(
                "[JellyNotif] [{Type}] '{Title}' → {Target} (persisted).",
                entity.Type, entity.Title, entity.TargetUserId);

            // (C) Push WebSocket via ISessionManager
            var pushed = await PushToSessionsAsync(entity, ct).ConfigureAwait(false);

            // (D) Marquer comme envoyé si le push a atteint au moins une session
            if (pushed)
                await _repo.MarkAsSentAsync(entity.Id, ct).ConfigureAwait(false);

            // Purge automatique si configurée
            var config = GetConfig();
            if (config.RetentionDays > 0)
                await _repo.PurgeOldAsync(config.RetentionDays, ct).ConfigureAwait(false);

            return entity;
        }

        // ──────────────────────────────────────────────────────────────
        //  Fallback API — GET /Notification/List
        // ──────────────────────────────────────────────────────────────

        /// <summary>Retourne les notifications visibles par un utilisateur (fallback polling).</summary>
        public Task<List<NotificationDto>> GetForUserAsync(string userId, CancellationToken ct = default)
            => _repo.GetForUserAsync(userId, ct);

        /// <summary>Retourne toutes les notifications pour le tableau de bord admin.</summary>
        public Task<List<AdminNotificationDto>> GetAllAsync(CancellationToken ct = default)
            => _repo.GetAllAsync(ct);

        /// <summary>Marque une notification comme lue.</summary>
        public Task<bool> MarkAsReadAsync(Guid notifId, string userId, CancellationToken ct = default)
            => _repo.MarkAsReadAsync(notifId, userId, ct);

        // ──────────────────────────────────────────────────────────────
        //  Push interne — ISessionManager
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Pousse la notification via le mécanisme natif Jellyfin.
        /// Utilise <see cref="ISessionManager.SendMessageCommand"/> par session
        /// pour afficher un toast natif côté client.
        /// Retourne true si au moins une session a été atteinte.
        /// </summary>
        private async Task<bool> PushToSessionsAsync(
            NotificationEntity entity,
            CancellationToken ct)
        {
            try
            {
                var sessions = SessionManager.Sessions
                    .Where(s => s.UserId != Guid.Empty && s.IsActive)
                    .ToList();

                // Filtrer par utilisateur cible
                if (!string.Equals(entity.TargetUserId, "All", StringComparison.OrdinalIgnoreCase))
                {
                    if (!Guid.TryParse(entity.TargetUserId, out var targetGuid))
                    {
                        _logger.LogWarning("[JellyNotif] TargetUserId invalide : '{Value}'.", entity.TargetUserId);
                        return false;
                    }
                    sessions = sessions.Where(s => s.UserId == targetGuid).ToList();
                }

                if (sessions.Count == 0)
                {
                    _logger.LogDebug("[JellyNotif] Aucune session active pour le push.");
                    return false;
                }

                // Payload : MessageCommand = toast natif Jellyfin
                var payload = new MessageCommand
                {
                    Header    = $"[{entity.Type}] {entity.Title}",
                    Text      = entity.Message,
                    TimeoutMs = 4000
                };

                int pushed = 0;
                foreach (var session in sessions)
                {
                    try
                    {
                        await SessionManager.SendMessageCommand(
                            session.Id,   // controllingSessionId (self)
                            session.Id,   // sessionId cible
                            payload,
                            ct
                        ).ConfigureAwait(false);
                        pushed++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "[JellyNotif] Push échoué pour session {SessionId}.", session.Id);
                    }
                }

                _logger.LogInformation(
                    "[JellyNotif] Push toast → {Pushed}/{Total} session(s).",
                    pushed, sessions.Count);
                return pushed > 0;
            }
            catch (Exception ex)
            {
                // Push non-fatal : la notification est déjà en DB, le client va la récupérer en polling
                _logger.LogWarning(ex, "[JellyNotif] Push WebSocket échoué (fallback polling actif).");
                return false;
            }
        }

        // ──────────────────────────────────────────────────────────────
        //  Configuration
        // ──────────────────────────────────────────────────────────────

        private static PluginConfiguration GetConfig()
            => Plugin.Instance?.Configuration ?? new PluginConfiguration();
    }
}
