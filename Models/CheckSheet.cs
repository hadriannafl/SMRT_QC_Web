using System.ComponentModel.DataAnnotations;

namespace SMRT_QC_Web.Models;

/// <summary>
/// Defines the inspection check items for a specific part number.
/// Multiple check items can exist for a single part, forming its quality checklist.
/// </summary>
public class CheckSheet
{
    /// <summary>Primary key (auto-increment).</summary>
    public int Id { get; set; }

    /// <summary>Foreign reference to the part this check item belongs to.</summary>
    [Required(ErrorMessage = "Part Number wajib diisi.")]
    [StringLength(100)]
    [Display(Name = "Part Number")]
    public string PartNumber { get; set; } = string.Empty;

    /// <summary>Name or description of the inspection check item.</summary>
    [Required(ErrorMessage = "Check Item wajib diisi.")]
    [StringLength(200)]
    [Display(Name = "Check Item")]
    public string CheckItem { get; set; } = string.Empty;

    /// <summary>Acceptance specification or tolerance for this check item.</summary>
    [StringLength(300)]
    [Display(Name = "Spesifikasi")]
    public string? Specification { get; set; }

    /// <summary>Inspection method used (e.g., Visual, Caliper, Go/NoGo).</summary>
    [StringLength(100)]
    [Display(Name = "Metode")]
    public string? Method { get; set; }

    /// <summary>Measurement unit (e.g., mm, kg, pcs).</summary>
    [StringLength(50)]
    [Display(Name = "Satuan")]
    public string? Unit { get; set; }

    /// <summary>Record creation timestamp.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
