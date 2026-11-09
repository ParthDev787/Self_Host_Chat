    using System.ComponentModel.DataAnnotations;

namespace API.DTOs;

public class RegisterDto
{
    [Required]
    [MaxLength(50)]
    [RegularExpression(@"^[a-zA-Z0-9_\-\.]+$", ErrorMessage = "Username can only contain alphanumeric characters, underscores, hyphens, and periods")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 4, ErrorMessage = "Password must be between 4 and 100 characters long")]
    public string Password { get; set; } = string.Empty;
}
