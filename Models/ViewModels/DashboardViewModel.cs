using SMRT_QC_Web.Models;

namespace SMRT_QC_Web.Models.ViewModels;

/// <summary>
/// View model for the Dashboard. Aggregates statistics and recent activity
/// data displayed on the main landing page after login.
/// </summary>
public class DashboardViewModel
{
    // ─── Stat Cards ───────────────────────────────────────────────────────────

    /// <summary>Total inspection records in the database.</summary>
    public int TotalInspections { get; set; }

    /// <summary>Inspections currently in CHECK status (awaiting QC).</summary>
    public int PendingInspections { get; set; }

    /// <summary>Inspections awaiting supervisor/manager VALIDATION.</summary>
    public int PendingValidation { get; set; }

    /// <summary>Open NCN records not yet resolved (status != DONE).</summary>
    public int OpenNcns { get; set; }

    /// <summary>Total NCN records in the database.</summary>
    public int TotalNcns { get; set; }

    /// <summary>Total number of registered parts in the master table.</summary>
    public int TotalParts { get; set; }

    /// <summary>Unread notifications for the currently logged-in user's role.</summary>
    public int UnreadNotifications { get; set; }

    // ─── Lists ────────────────────────────────────────────────────────────────

    /// <summary>The 10 most recent inspection records for the activity table.</summary>
    public List<InspectionRecord> RecentInspections { get; set; } = new();

    /// <summary>Unread notifications targeted at the current user's role.</summary>
    public List<Notification> Notifications { get; set; } = new();

    /// <summary>The 5 most recently created NCN records.</summary>
    public List<NcnRecord> RecentNcns { get; set; } = new();
}
