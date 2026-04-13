using System.ComponentModel.DataAnnotations;

namespace SMRT_QC_Web.Models;

/// <summary>
/// Master data for parts/components that are inspected in the QC process.
/// Each part is uniquely identified by its PartNumber.
/// </summary>
public class Part
{
    /// <summary>Primary key (auto-increment).</summary>
    public int Id { get; set; }

    /// <summary>Unique part identifier code (e.g., "PN-001-A").</summary>
    [Required(ErrorMessage = "Part Number wajib diisi.")]
    [StringLength(100, ErrorMessage = "Part Number maksimal 100 karakter.")]
    [Display(Name = "Part Number")]
    public string PartNumber { get; set; } = string.Empty;

    /// <summary>Human-readable name of the part.</summary>
    [Required(ErrorMessage = "Part Name wajib diisi.")]
    [StringLength(200, ErrorMessage = "Part Name maksimal 200 karakter.")]
    [Display(Name = "Part Name")]
    public string PartName { get; set; } = string.Empty;

    /// <summary>Classification category for grouping parts.</summary>
    [StringLength(100)]
    [Display(Name = "Kategori")]
    public string? Category { get; set; }

    /// <summary>Technical specification or drawing reference.</summary>
    [StringLength(500)]
    [Display(Name = "Spesifikasi")]
    public string? Spec { get; set; }

    /// <summary>Record creation timestamp.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Last update timestamp.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
