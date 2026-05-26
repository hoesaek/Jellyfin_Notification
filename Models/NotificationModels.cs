using System;
using System.Text.Json.Serialization;

namespace Jellyfin_notification.Models
{
    // ================================================================
    // ENTITÉ — Source de vérité SQLite
    // ================================================================

    /// <summary>
    /// Représente une ligne de la table <c>Notifications</c> en base.
    /// </summary>
    public class NotificationEntity
    {
        public Guid   Id           { get; set; } = Guid.NewGuid();
        public string TargetUserId { get; set; } = "All";
        public string Title        { get; set; } = string.Empty;
        public string Message      { get; set; } = string.Empty;
        public string Type         { get; set; } = "Info";
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
        public bool   IsSent       { get; set; } = false;
    }

    // ================================================================
    // DTOs — Contrats exposés par l'API REST
    // ================================================================

    /// <summary>DTO renvoyé par GET /Notification/List (frontend utilisateur).</summary>
    public class NotificationDto
    {
        [JsonPropertyName("id")]     public string Id      { get; set; } = string.Empty;
        [JsonPropertyName("title")]  public string Title   { get; set; } = string.Empty;
        [JsonPropertyName("message")]public string Message { get; set; } = string.Empty;
        [JsonPropertyName("type")]   public string Type    { get; set; } = "Info";
        [JsonPropertyName("date")]   public string Date    { get; set; } = string.Empty;
        [JsonPropertyName("isRead")] public bool   IsRead  { get; set; }
    }

    /// <summary>DTO renvoyé par GET /Notification/Admin/History (dashboard admin).</summary>
    public class AdminNotificationDto
    {
        [JsonPropertyName("id")]          public string Id           { get; set; } = string.Empty;
        [JsonPropertyName("title")]       public string Title        { get; set; } = string.Empty;
        [JsonPropertyName("message")]     public string Message      { get; set; } = string.Empty;
        [JsonPropertyName("type")]        public string Type         { get; set; } = "Info";
        [JsonPropertyName("targetUserId")]public string TargetUserId { get; set; } = "All";
        [JsonPropertyName("dateCreated")] public string DateCreated  { get; set; } = string.Empty;
        [JsonPropertyName("readCount")]   public int    ReadCount    { get; set; }
        [JsonPropertyName("isSent")]      public bool   IsSent       { get; set; }
    }

    // ================================================================
    // REQUÊTES — Body des POST
    // ================================================================

    /// <summary>Body attendu par POST /Notification/Send.</summary>
    public class SendNotificationRequest
    {
        [JsonPropertyName("title")]
        public string Title        { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message      { get; set; } = string.Empty;

        [JsonPropertyName("targetUserId")]
        public string TargetUserId { get; set; } = "All";

        [JsonPropertyName("type")]
        public string Type         { get; set; } = "Info";
    }
}
