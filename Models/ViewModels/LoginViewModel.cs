using System.ComponentModel.DataAnnotations;

namespace SMRT_QC_Web.Models.ViewModels;

/// <summary>
/// View model for the Login page. Contains only the credentials
/// needed for authentication — no sensitive DB model is exposed.
/// </summary>
public class LoginViewModel
{
    /// <summary>The username entered on the login form.</summary>
    [Required(ErrorMessage = "Username wajib diisi.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Username harus 3-100 karakter.")]
    [Display(Name = "Username")]
    public string UserName { get; set; } = string.Empty;

    /// <summary>The plain-text password entered on the login form (verified against BCrypt hash).</summary>
    [Required(ErrorMessage = "Password wajib diisi.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    /// <summary>When true, the auth cookie persists across browser sessions.</summary>
    [Display(Name = "Ingat Saya")]
    public bool RememberMe { get; set; } = false;
}
