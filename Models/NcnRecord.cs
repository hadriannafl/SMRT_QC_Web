using System.ComponentModel.DataAnnotations;

namespace SMRT_QC_Web.Models;

/// <summary>
/// Non-Conformance Notice (NCN) record created when a defect is identified
/// during inspection. Can include a photo captured via browser camera.
/// Status flows: CHECK → PROCESS → DONE.
/// </summary>
public class NcnRecord
{
    /// <summary>Primary key (auto-increment).</summary>
    public int Id { get; set; }

    /// <summary>Unique NCN identifier number (e.g., "NCN-2024-001").</summary>
    [Required(ErrorMessage = "NCN Number wajib diisi.")]
    [StringLength(100)]
    [Display(Name = "NCN No")]
    public string NcnNo { get; set; } = string.Empty;

    /// <summary>Supply/batch number referencing the original inspection.</summary>
    [StringLength(100)]
    [Display(Name = "Supply Number")]
    public string? SupplyNumber { get; set; }

    /// <summary>Part number that has the defect.</summary>
    [StringLength(100)]
    [Display(Name = "Part Number")]
    public string? PartNumber { get; set; }

    /// <summary>Type or description of the defect found.</summary>
    [StringLength(200)]
    [Display(Name = "Jenis Defect")]
    public string? DefectType { get; set; }

    /// <summary>Quantity of defective units found.</summary>
    [Display(Name = "Jumlah Defect")]
    [Range(0, int.MaxValue, ErrorMessage = "Jumlah defect tidak boleh negatif.")]
    public int? DefectQty { get; set; }

    /// <summary>Name of the QC checker who identified the defect.</summary>
    [StringLength(100)]
    [Display(Name = "Checker")]
    public string? CheckerName { get; set; }

    /// <summary>Name of the supervisor/manager who approved the NCN.</summary>
    [StringLength(100)]
    [Display(Name = "Approver")]
    public string? ApproverName { get; set; }

    /// <summary>Current processing status: CHECK | PROCESS | DONE.</summary>
    [StringLength(20)]
    [Display(Name = "Status")]
    public string Status { get; set; } = "CHECK";

    /// <summary>
    /// Relative path to the uploaded defect photo stored under wwwroot/uploads/.
    /// Captured via browser getUserMedia API or uploaded from device.
    /// </summary>
    [StringLength(500)]
    [Display(Name = "Foto")]
    public string? PhotoPath { get; set; }

    /// <summary>Additional notes or corrective action details.</summary>
    [Display(Name = "Catatan")]
    public string? Notes { get; set; }

    /// <summary>Record creation timestamp.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Last update timestamp.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>Returns Tailwind badge CSS classes for the current NCN status.</summary>
    public string StatusBadgeClass => Status switch
    {
        "CHECK"   => "bg-yellow-100 text-yellow-700 border border-yellow-300",
        "PROCESS" => "bg-blue-100 text-blue-700 border border-blue-300",
        "DONE"    => "bg-green-100 text-green-700 border border-green-300",
        _         => "bg-gray-100 text-gray-600 border border-gray-300"
    };

    /// <summary>Returns true if a defect photo has been uploaded.</summary>
    public bool HasPhoto => !string.IsNullOrWhiteSpace(PhotoPath);
}
