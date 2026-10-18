using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

public record ErrorResponse(string Message);
public record MessageResponse(string Message);

public class RegisterRequest
{
    /// <example>Mmabatho Vilakazi</example>
    [Required, StringLength(101, MinimumLength = 3)]
    public string FullName { get; set; } = "";

    /// <example>mmabatho@example.com</example>
    [Required, EmailAddress, StringLength(100)]
    public string Email { get; set; } = "";

    /// <example>Passw0rd!123</example>
    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = "";

    /// <summary>Either "Organiser" or "Participant".</summary>
    /// <example>Participant</example>
    [Required]
    public string Role { get; set; } = "";

    /// <example>0821234567</example>
    [Required, StringLength(20, MinimumLength = 7)]
    public string PhoneNumber { get; set; } = "";

    /// <summary>Required only when registering as an Organiser (prevents anyone self-promoting to Organiser).</summary>
    public string? OrganiserCode { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    public string Password { get; set; } = "";
}

public class UserResponse
{
    public int UserId { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string? ProfilePictureUrl { get; set; }
}

public class UpdateProfileRequest
{
    /// <example>Mmabatho Vilakazi</example>
    [Required, StringLength(101, MinimumLength = 3)]
    public string FullName { get; set; } = "";

    /// <example>0821234567</example>
    [Required, StringLength(20, MinimumLength = 7)]
    public string PhoneNumber { get; set; } = "";
}



