using System.ComponentModel.DataAnnotations;

namespace LeaseLense.Web.Models.Account;

public sealed class RegisterViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [StringLength(510)]
    public string? DisplayName { get; init; }

    [Required]
    [DataType(DataType.Password)]
    [MinLength(8)]
    public string Password { get; init; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password))]
    public string ConfirmPassword { get; init; } = string.Empty;
}
