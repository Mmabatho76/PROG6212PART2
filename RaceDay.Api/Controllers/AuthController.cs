using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

/// <summary>Registration, login and logout (session based).</summary>
[Route("api/auth")]
[Tags("1. Authentication")]
public class AuthController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly IConfiguration _config;

    public AuthController(RaceDayDbContext db, IPasswordService passwords, IConfiguration config)
    {
        _db = db;
        _passwords = passwords;
        _config = config;
    }

    /// <summary>Register a new account as an Organiser or a Participant.</summary>
    /// <remarks>
    /// Body: fullName, email, password (min 8 chars), role ("Organiser" or "Participant"), phoneNumber.
    /// To stop anyone from simply choosing to be an Organiser, Organiser accounts must also supply the
    /// <c>organiserCode</c> configured on the server (appsettings: Registration:OrganiserCode).
    /// The password is salted and hashed before it is stored.
    /// </remarks>
    /// <response code="201">Account created - returns the user summary.</response>
    /// <response code="400">Validation errors (bad email, short password, invalid role or name).</response>
    /// <response code="403">Organiser registration code missing or wrong.</response>
    /// <response code="409">Email is already registered.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var role = RoleNames.Normalize(request.Role);
        if (role is null)
            return BadRequest(new ErrorResponse("Role must be either 'Organiser' or 'Participant'."));

        if (!NameHelper.TrySplit(request.FullName, out var name, out var surname))
            return BadRequest(new ErrorResponse("Please provide your name and surname (each up to 50 characters)."));

        if (role == RoleNames.Organiser)
        {
            var requiredCode = _config["Registration:OrganiserCode"];
            if (!string.IsNullOrEmpty(requiredCode) && request.OrganiserCode != requiredCode)
                return StatusCode(StatusCodes.Status403Forbidden,
                    new ErrorResponse("A valid organiserCode is required to register as an Organiser."));
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.AppUsers.AnyAsync(u => u.Email == email))
            return Conflict(new ErrorResponse("An account with this email already exists."));

        var user = new AppUser
        {
            UserName = name,
            UserSurname = surname,
            Email = email,
            PasswordHash = _passwords.Hash(request.Password),
            Role = role,
            PhoneNumber = request.PhoneNumber.Trim()
        };
        _db.AppUsers.Add(user);
        await _db.SaveChangesAsync();

        return Created("/api/users/me", ToResponse(user));
    }

    /// <summary>Log in with email and password. A server-side session storing the user id and role is started.</summary>
    /// <response code="200">Logged in - returns the user summary and role.</response>
    /// <response code="400">Missing or badly formatted fields.</response>
    /// <response code="401">Invalid credentials.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.Email == email);

        if (user is null || !_passwords.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new ErrorResponse("Invalid email or password."));

        await HttpContext.Session.LoadAsync();
        HttpContext.Session.Clear(); // start a clean session (avoids session fixation)
        HttpContext.Session.SetInt32(SessionKeys.UserId, user.UserID);
        HttpContext.Session.SetString(SessionKeys.Role, user.Role);
        await HttpContext.Session.CommitAsync();

        return Ok(ToResponse(user));
    }

    /// <summary>End the current session.</summary>
    /// <response code="200">Logout confirmed.</response>
    /// <response code="401">No active session.</response>
    [HttpPost("logout")]
    [SessionAuthorize]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return Ok(new MessageResponse("Logged out successfully."));
    }

    internal static UserResponse ToResponse(AppUser u) => new()
    {
        UserId = u.UserID,
        FullName = $"{u.UserName} {u.UserSurname}",
        Email = u.Email,
        Role = u.Role,
        PhoneNumber = u.PhoneNumber
    };
}

