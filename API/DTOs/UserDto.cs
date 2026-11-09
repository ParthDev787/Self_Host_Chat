using System.ComponentModel.DataAnnotations;

namespace API.DTOs;

public class UserDto
{
    public required string Username { get; set; }
    public required string Token { get; set; }
    public string Theme { get; set; } = "dark";
}

public record UpdateThemeDto(
    [Required][MaxLength(50)] string Username,
    [Required][MaxLength(30)] string Theme
);
