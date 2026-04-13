using System.ComponentModel.DataAnnotations;

namespace SMRT_QC_Web.Models;

/// <summary>
/// Real-time notification record sent to specific roles via SignalR.
/// Persisted in the database so users can see past notifications on login.
/// </summary>
public class Notification
{
    /// <summary>Primary key (auto-increment).</summary>
    public int Id { get; set; }

    /// <summary>Supply/batch number related to this notification.</summary>
    [StringLength(100)]
    public string? SuppNumber { get; set; }

    /// <summary>Part number related to this notification.</summary>
    [StringLength(100)]
    public string? PartNumber { get; set; }

    /// <summary>Target role/position that should receive this notification (e.g., "SUPERVISOR").</summary>
    [StringLength(20)]
    public string? Position { get; set; }

    /// <summary>Notification message content.</summary>
    [StringLength(500)]
    public string? Notif { get; set; }

    /// <summary>Whether the target user has read/acknowledged this notification.</summary>
    public bool IsRead { get; set; } = false;

    /// <summary>Timestamp when the notification was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // ─── Helper ──────────────────────────────────────────────────────────────

    /// <summary>Formatted creation time for display in notification list.</summary>
    public string TimeAgo
    {
        get
        {
            var diff = DateTime.Now - CreatedAt;
            if (diff.TotalMinutes < 1) return "Baru saja";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} menit lalu";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} jam lalu";
            return CreatedAt.ToString("dd MMM yyyy HH:mm");
        }
    }
}
