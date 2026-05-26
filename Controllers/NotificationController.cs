using System;
using System.Net.Mime;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Jellyfin_notification.Models;
using Jellyfin_notification.Services;

namespace Jellyfin_notification.Controllers
{
    /// <summary>
    /// API REST du plugin Jellyfin Notification.
    ///
    ///   POST /Notification/Send            → Admin  — Créer + push une notification
    ///   GET  /Notification/Admin/History   → Admin  — Historique complet
    ///   GET  /Notification/List            → User   — Notifs de l'utilisateur courant
    ///   POST /Notification/MarkAsRead/{id} → User   — Marquer comme lue
    ///
    /// Ce contrôleur est un thin adapter : aucune logique métier ici,
    /// tout est délégué à <see cref="NotificationService"/>.
    /// </summary>
    [ApiController]
    [Route("Notification")]
    [Produces(MediaTypeNames.Application.Json)]
    public class NotificationController : ControllerBase
    {
        private readonly NotificationService _svc;

        /// <summary>Injection du service via DI.</summary>
        public NotificationController(NotificationService svc) => _svc = svc;

        // ================================================================
        // POST /Notification/Send  (Admin)
        // ================================================================

        /// <summary>Crée et pousse immédiatement une notification (Outbox).</summary>
        [HttpPost("Send")]
        [Authorize(Policy = "RequiresElevation")]
        [ProducesResponseType(typeof(NotificationEntity), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Send(
            [FromBody] SendNotificationRequest request,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return BadRequest("Le champ 'title' est obligatoire.");
            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest("Le champ 'message' est obligatoire.");
            if (request.Title.Length > 120)
                return BadRequest("Le titre ne doit pas dépasser 120 caractères.");
            if (request.Message.Length > 2000)
                return BadRequest("Le message ne doit pas dépasser 2000 caractères.");
            if (!string.Equals(request.TargetUserId, "All", StringComparison.OrdinalIgnoreCase)
                && !Guid.TryParse(request.TargetUserId, out _))
                return BadRequest("TargetUserId doit être 'All' ou un GUID valide.");

            var entity = await _svc.SendAsync(request, ct).ConfigureAwait(false);
            return StatusCode(StatusCodes.Status201Created, entity);
        }

        // ================================================================
        // GET /Notification/Admin/History  (Admin)
        // ================================================================

        /// <summary>Retourne l'historique complet pour le tableau de bord admin.</summary>
        [HttpGet("Admin/History")]
        [Authorize(Policy = "RequiresElevation")]
        [ProducesResponseType(typeof(System.Collections.Generic.List<AdminNotificationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAdminHistory(CancellationToken ct)
        {
            var result = await _svc.GetAllAsync(ct).ConfigureAwait(false);
            return Ok(result);
        }

        // ================================================================
        // GET /Notification/List  (User connecté — fallback Outbox)
        // ================================================================

        /// <summary>
        /// Retourne les notifications de l'utilisateur courant.
        /// C'est le fallback polling du pattern Outbox
        /// (pour les clients qui étaient offline lors du push WebSocket).
        /// </summary>
        [HttpGet("List")]
        [Authorize]
        [ProducesResponseType(typeof(System.Collections.Generic.List<NotificationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetList(CancellationToken ct)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var result = await _svc.GetForUserAsync(userId, ct).ConfigureAwait(false);
            return Ok(result);
        }

        // ================================================================
        // POST /Notification/MarkAsRead/{notifId}  (User connecté)
        // ================================================================

        /// <summary>Marque une notification comme lue par l'utilisateur courant.</summary>
        [HttpPost("MarkAsRead/{notifId:guid}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> MarkAsRead(
            [FromRoute] Guid notifId,
            CancellationToken ct)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var found = await _svc.MarkAsReadAsync(notifId, userId, ct).ConfigureAwait(false);
            return found ? NoContent() : NotFound();
        }

        // ──────────────────────────────────────────────────────────────
        //  Helper
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Extrait l'ID utilisateur depuis les claims du JWT/session Jellyfin.
        /// 
        /// Jellyfin 10.11.x utilise un claim personnalisé "Jellyfin-UserId"
        /// qui n'est ni ClaimTypes.NameIdentifier ni "sub". On le cherche
        /// en priorité, avec fallback sur les claims standards pour la
        /// compatibilité future.
        /// </summary>
        private string GetUserId()
        {
            // Priorité 1 : claim spécifique Jellyfin 10.11.x
            var jellyfinClaim = User.FindFirst("Jellyfin-UserId")?.Value;
            if (!string.IsNullOrEmpty(jellyfinClaim))
                return jellyfinClaim;

            // Priorité 2 : claim standard .NET
            var nameid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(nameid))
                return nameid;

            // Priorité 3 : claim JWT standard
            return User.FindFirst("sub")?.Value ?? string.Empty;
        }
    }
}
