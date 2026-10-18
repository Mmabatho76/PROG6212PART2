```csharp
// Import ASP.NET Core classes used to create API responses and handle HTTP requests.
using Microsoft.AspNetCore.Mvc;

// Import Entity Framework Core methods used to query the database asynchronously.
using Microsoft.EntityFrameworkCore;

// Import the application's database context for accessing stored data.
using RaceDay.Api.Data;

// Import DTO classes used to receive requests and return responses.
using RaceDay.Api.Dtos;

// Import the AppUser model representing a registered user.
using RaceDay.Api.Models;

// Import security classes used for password hashing, role handling and session management.
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Handles user registration, login and logout using server-side sessions.
/// </summary>
[Route("api/auth")] // Sets the base URL for all authentication endpoints in this controller.
[Tags("1. Authentication")] // Groups these endpoints under Authentication in Swagger documentation.
public class AuthController : ApiControllerBase
{
    // Stores the database context so the controller can access registered users.
    private readonly RaceDayDbContext _db;

    // Provides methods for hashing passwords and verifying submitted passwords.
    private readonly IPasswordService _passwords;

    // Reads application configuration settings, including the organiser registration code.
    private readonly IConfiguration _config;

    // Constructor: receives the required services through dependency injection.
    public AuthController(
        RaceDayDbContext db,
        IPasswordService passwords,
        IConfiguration config)
    {
        // Store the database context for use throughout the controller.
        _db = db;

        // Store the password service for secure password handling.
        _passwords = passwords;

        // Store the configuration service for reading application settings.
        _config = config;
    }

    /// <summary>
    /// Registers a new account as an Organiser or a Participant.
    /// </summary>
    /// <remarks>
    /// The request must contain the user's full name, email, password,
    /// role and phone number.
    ///
    /// The password must contain at least eight characters.
    /// Organiser registration also requires a valid organiser code when
    /// a code has been configured in the application settings.
    ///
    /// Passwords are hashed before they are stored in the database.
    /// </remarks>
    /// <response code="201">The account was created successfully.</response>
    /// <response code="400">The supplied information is invalid.</response>
    /// <response code="403">The organiser registration code is invalid or missing.</response>
    /// <response code="409">An account with the supplied email already exists.</response>

    // Accepts POST requests sent to /api/auth/register.
    [HttpPost("register")]

    // Documents the possible HTTP responses for this endpoint in Swagger.
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        // Convert the submitted role into the application's standard role format.
        // If the role is not recognised, Normalize returns null.
        var role = RoleNames.Normalize(request.Role);

        // Reject the request if the user selected an unsupported role.
        if (role is null)
            return BadRequest(new ErrorResponse(
                "Role must be either 'Organiser' or 'Participant'."));

        // Separate the submitted full name into a first name and surname.
        // The helper also checks whether the name meets the required format.
        if (!NameHelper.TrySplit(request.FullName, out var name, out var surname))
            return BadRequest(new ErrorResponse(
                "Please provide your name and surname (each up to 50 characters)."));

        // Organisers must pass an additional security check before registration.
        if (role == RoleNames.Organiser)
        {
            // Read the required organiser code from the application configuration.
            var requiredCode = _config["Registration:OrganiserCode"];

            // If a code is configured, reject the request when the submitted code
            // does not match it. This prevents users from freely choosing the
            // Organiser role without knowing the required registration code.
            if (!string.IsNullOrEmpty(requiredCode) &&
                request.OrganiserCode != requiredCode)
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new ErrorResponse(
                        "A valid organiserCode is required to register as an Organiser."));
        }

        // Remove unnecessary spaces and convert the email to lowercase.
        // This helps prevent the same email from being registered in different cases.
        var email = request.Email.Trim().ToLowerInvariant();

        // Check whether the email address already belongs to a registered user.
        // AnyAsync checks for a matching record without loading all users.
        if (await _db.AppUsers.AnyAsync(u => u.Email == email))
            return Conflict(new ErrorResponse(
                "An account with this email already exists."));

        // Create a new user object using the validated registration information.
        var user = new AppUser
        {
            // Save the user's first name.
            UserName = name,

            // Save the user's surname.
            UserSurname = surname,

            // Save the normalised email address.
            Email = email,

            // Hash the password before storing it.
            // The original plain-text password is not saved in this property.
            PasswordHash = _passwords.Hash(request.Password),

            // Store the validated user role.
            Role = role,

            // Remove unnecessary spaces from the phone number before saving it.
            PhoneNumber = request.PhoneNumber.Trim()
        };

        // Add the new user to the database context.
        // The record is not saved permanently until SaveChangesAsync is called.
        _db.AppUsers.Add(user);

        // Save the new account to the database asynchronously.
        await _db.SaveChangesAsync();

        // Return HTTP 201 Created and the user's public information.
        // The response does not expose the stored password hash.
        return Created("/api/users/me", ToResponse(user));
    }

    /// <summary>
    /// Logs in a user using their email address and password.
    /// A server-side session stores the user's ID and role.
    /// </summary>
    /// <response code="200">Login was successful.</response>
    /// <response code="400">The request contains invalid fields.</response>
    /// <response code="401">The email address or password is incorrect.</response>

    // Accepts POST requests sent to /api/auth/login.
    [HttpPost("login")]

    // Document the successful and unsuccessful responses in Swagger.
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // Normalise the submitted email so it matches the format used during registration.
        var email = request.Email.Trim().ToLowerInvariant();

        // Search the database for a user with the submitted email address.
        // FirstOrDefaultAsync returns the matching user or null if no match exists.
        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.Email == email);

        // Reject the login if the email does not exist or the password is incorrect.
        // Verify compares the submitted password against the stored password hash.
        // Using the same error message avoids revealing whether an email is registered.
        if (user is null ||
            !_passwords.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new ErrorResponse(
                "Invalid email or password."));

        // Load the current session before modifying its contents.
        await HttpContext.Session.LoadAsync();

        // Remove any existing session values before starting a new authenticated session.
        // This helps prevent session fixation by avoiding reuse of previous session data.
        HttpContext.Session.Clear();

        // Store the logged-in user's ID in the server-side session.
        // Other endpoints can use this ID to identify the current user.
        HttpContext.Session.SetInt32(SessionKeys.UserId, user.UserID);

        // Store the user's role in the session.
        // This can be used by the application when checking role-based permissions.
        HttpContext.Session.SetString(SessionKeys.Role, user.Role);

        // Save the session changes so the session information is persisted.
        await HttpContext.Session.CommitAsync();

        // Return HTTP 200 OK with the user's public account information.
        return Ok(ToResponse(user));
    }

    /// <summary>
    /// Logs out the currently authenticated user by clearing their session.
    /// </summary>
    /// <response code="200">Logout was successful.</response>
    /// <response code="401">The request is not authenticated.</response>

    // Accepts POST requests sent to /api/auth/logout.
    [HttpPost("logout")]

    // Require a valid session before allowing the user to log out.
    [SessionAuthorize]

    // Document the possible responses in Swagger.
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public IActionResult Logout()
    {
        // Remove the stored user ID, role and other session data.
        // This ends the application's current authenticated session.
        HttpContext.Session.Clear();

        // Return a success message confirming that the user has logged out.
        return Ok(new MessageResponse("Logged out successfully."));
    }

    // Converts an AppUser database model into a UserResponse DTO.
    // This ensures that only the intended user information is returned by the API.
    // The password hash is excluded from the response.
    internal static UserResponse ToResponse(AppUser u) => new()
    {
        // Include the user's unique database ID.
        UserId = u.UserID,

        // Combine the user's first name and surname into one full-name value.
        FullName = $"{u.UserName} {u.UserSurname}",

        // Include the user's email address.
        Email = u.Email,

        // Include the user's assigned role.
        Role = u.Role,

        // Include the user's phone number.
        PhoneNumber = u.PhoneNumber
    };
}
```
