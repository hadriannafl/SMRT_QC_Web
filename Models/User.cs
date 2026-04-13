using System.ComponentModel.DataAnnotations;

namespace SMRT_QC_Web.Models;

/// <summary>
/// Represents a system user with role-based access.
/// Position maps directly to ASP.NET Core authorization roles.
/// </summary>
public class User
{
    /// <summary>Primary key (auto-increment).</summary>
    public int Id { get; set; }

    /// <summary>Unique login username.</summary>
    [Required(ErrorMessage = "Username wajib diisi.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Username harus 3-100 karakter.")]
    [Display(Name = "Username")]
    public string UserName { get; set; } = string.Empty;

    /// <summary>BCrypt-hashed password (never stored as plain text).</summary>
    [Required(ErrorMessage = "Password wajib diisi.")]
    [StringLength(255)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    /// <summary>User role: ADMIN | MANAGER | SUPERVISOR | STAFF.</summary>
    [Required(ErrorMessage = "Posisi wajib dipilih.")]
    [StringLength(20)]
    [Display(Name = "Posisi")]
    public string Position { get; set; } = "STAFF";

    /// <summary>Record creation timestamp.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Last update timestamp.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>Returns badge CSS class based on the user's position/role.</summary>
    public string PositionBadgeClass => Position switch
    {
        "ADMIN"      => "bg-red-100 text-red-700",
        "MANAGER"    => "bg-purple-100 text-purple-700",
        "SUPERVISOR" => "bg-blue-100 text-blue-700",
        "STAFF"      => "bg-green-100 text-green-700",
        _            => "bg-gray-100 text-gray-700"
    };
}
