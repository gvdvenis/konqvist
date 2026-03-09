using System.ComponentModel.DataAnnotations;

namespace Konqvist.Admin.Features.Auth;

public sealed class AdminCredentialsOptions
{
    public const string SectionName = "Admin";

    [Required]
    public string Username { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}
