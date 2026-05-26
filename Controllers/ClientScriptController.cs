using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin_notification.Controllers
{
    /// <summary>
    /// Sert le script JavaScript client embarqué dans la DLL.
    ///
    /// Séparé de NotificationController pour éviter le conflit de Content-Type :
    ///   NotificationController → [Produces("application/json")]
    ///   ClientScriptController → Produces "application/javascript"
    ///
    /// Le script est chargé via le &lt;script&gt; injecté dans index.html par Plugin.cs.
    /// Route : GET /JellyNotif/client?v={version}
    ///   → v= est utilisé comme cache-buster côté navigateur
    ///   → le serveur répond avec Cache-Control: max-age=3600
    /// </summary>
    [ApiController]
    [AllowAnonymous]
    public class ClientScriptController : ControllerBase
    {
        // ================================================================
        // GET /JellyNotif/client  (Public — chargé dans index.html)
        // ================================================================

        /// <summary>
        /// Retourne le script JavaScript client embarqué.
        /// AllowAnonymous : ce script est chargé avant toute authentification.
        /// </summary>
        // Cache en mémoire : le script ne change pas à runtime
        private static readonly Lazy<byte[]?> CachedScript = new(() =>
        {
            const string resourceName = "Jellyfin_notification.ClientScript.notif-client.js";
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(resourceName);
            if (stream is null) return null;

            using var ms = new System.IO.MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        });

        [HttpGet("/JellyNotif/client")]
        [Produces("application/javascript")]
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult GetClientScript()
        {
            var bytes = CachedScript.Value;
            if (bytes is null)
                return NotFound("Script client introuvable dans la DLL.");

            return File(bytes, "application/javascript; charset=utf-8");
        }
    }
}
