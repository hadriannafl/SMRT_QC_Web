using System.ComponentModel.DataAnnotations;

namespace SMRT_QC_Web.Models;

/// <summary>
/// Represents a single inspection record (one line in a PO/supply inspection batch).
/// Status flows: CHECK → VALIDATION → MAKE NCN → DONE.
/// </summary>
public class InspectionRecord
{
    /// <summary>Primary key (auto-increment).</summary>
    public int Id { get; set; }

    /// <summary>Supply/delivery number referencing the incoming batch.</summary>
    [Required(ErrorMessage = "Supply Number wajib diisi.")]
    [StringLength(100)]
    [Display(Name = "Supply Number")]
    public string SupplyNumber { get; set; } = string.Empty;

    /// <summary>Date of the supply/delivery.</summary>
    [Display(Name = "Tanggal Supply")]
    [DataType(DataType.Date)]
    public DateTime? SupplyDate { get; set; }

    /// <summary>Production or inspection line identifier.</summary>
    [StringLength(50)]
    [Display(Name = "Line Number")]
    public string? LineNumber { get; set; }

    /// <summary>Part number being inspected (references parts master).</summary>
    [StringLength(100)]
    [Display(Name = "Part Number")]
    public string? PartNumber { get; set; }

    /// <summary>Purchase order number tied to this inspection batch.</summary>
    [StringLength(100)]
    [Display(Name = "PO Number")]
    public string? PoNumber { get; set; }

    /// <summary>Operator who performed the inspection.</summary>
    [StringLength(100)]
    [Display(Name = "Operator")]
    public string? Operator { get; set; }

    /// <summary>Category of items being inspected.</summary>
    [StringLength(100)]
    [Display(Name = "Item Category")]
    public string? ItemCategory { get; set; }

    /// <summary>NCN number if a non-conformance was raised from this record.</summary>
    [StringLength(100)]
    [Display(Name = "NCN No")]
    public string? NcnNo { get; set; }

    /// <summary>Name of the quality checker who inspected.</summary>
    [StringLength(100)]
    [Display(Name = "Checker")]
    public string? ChekName { get; set; }

    /// <summary>Name of the approver who validated this record.</summary>
    [StringLength(100)]
    [Display(Name = "Approver")]
    public string? AppvName { get; set; }

    /// <summary>Validation result text (e.g., "APPROVED", "REJECTED").</summary>
    [StringLength(50)]
    [Display(Name = "Validasi")]
    public string? Validasi { get; set; }

    /// <summary>
    /// Current workflow status: CHECK | VALIDATION | MAKE NCN | DONE.
    /// </summary>
    [StringLength(30)]
    [Display(Name = "Status")]
    public string? NcnStatus { get; set; } = "CHECK";

    /// <summary>Record creation timestamp.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Last update timestamp.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>Returns Tailwind badge CSS classes for the current NCN status.</summary>
    public string StatusBadgeClass => NcnStatus switch
    {
        "CHECK"      => "bg-yellow-100 text-yellow-700 border border-yellow-300",
        "VALIDATION" => "bg-blue-100 text-blue-700 border border-blue-300",
        "MAKE NCN"   => "bg-red-100 text-red-700 border border-red-300",
        "DONE"       => "bg-green-100 text-green-700 border border-green-300",
        _            => "bg-gray-100 text-gray-600 border border-gray-300"
    };
}
