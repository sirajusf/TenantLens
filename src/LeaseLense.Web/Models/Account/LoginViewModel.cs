using System.ComponentModel.DataAnnotations;

namespace LeaseLense.Web.Models.Account;

public sealed class LoginViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; init; } = string.Empty;

    public bool RememberMe { get; init; }
    public string? ReturnUrl { get; init; }
}
