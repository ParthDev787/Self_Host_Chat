using System.ComponentModel.DataAnnotations;

namespace API.DTOs;

public class RegisterDto
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 4, ErrorMessage = "Password must be at least 4 characters long")]
    public string Password { get; set; } = string.Empty;
}
