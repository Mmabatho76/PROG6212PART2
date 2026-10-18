using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

/// <summary>View and update the logged-in user's own profile.</summary>
[Route("api/users")]
[Tags("2. User Profile")]
[SessionAuthorize]
public class UsersController : ApiControllerBase
{
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    private const long MaxPictureBytes = 2 * 1024 * 1024;

    private readonly RaceDayDbContext _db;
    private readonly IWebHostEnvironment _env;

    public UsersController(RaceDayDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    /// <summary>Get the logged-in user's profile.</summary>
    /// <response code="200">Profile object.</response>
    /// <response code="401">No active session.</response>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMe()
    {
        var user = await _db.AppUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserID == CurrentUserId);
        if (user is null) return Unauthorized(new ErrorResponse("Your account no longer exists."));

        var response = AuthController.ToResponse(user);
        response.ProfilePictureUrl = FindPictureUrl(user.UserID);
        return Ok(response);
    }

    /// <summary>Update the logged-in user's name and phone number.</summary>
    /// <response code="200">Updated profile.</response>
    /// <response code="400">Validation errors.</response>
    /// <response code="401">No active session.</response>
    [HttpPut("me")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request)
    {
        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.UserID == CurrentUserId);
        if (user is null) return Unauthorized(new ErrorResponse("Your account no longer exists."));

        if (!NameHelper.TrySplit(request.FullName, out var name, out var surname))
            return BadRequest(new ErrorResponse("Please provide your name and surname (each up to 50 characters)."));

        user.UserName = name;
        user.UserSurname = surname;
        user.PhoneNumber = request.PhoneNumber.Trim();
        await _db.SaveChangesAsync();

        var response = AuthController.ToResponse(user);
        response.ProfilePictureUrl = FindPictureUrl(user.UserID);
        return Ok(response);
    }

    /// <summary>Upload or replace the logged-in user's profile picture (jpg, png, gif or webp, max 2 MB).</summary>
    /// <remarks>Send as multipart/form-data with a single file field named "file".</remarks>
    /// <response code="200">Returns the URL of the new picture.</response>
    /// <response code="400">Invalid image file.</response>
    /// <response code="401">No active session.</response>
    [HttpPut("me/profile-picture")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadProfilePicture(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ErrorResponse("Please choose an image file."));
        if (file.Length > MaxPictureBytes)
            return BadRequest(new ErrorResponse("The image must be 2 MB or smaller."));

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext) || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new ErrorResponse("Only jpg, jpeg, png, gif or webp images are allowed."));

        var dir = PicturesDirectory();
        Directory.CreateDirectory(dir);
        foreach (var old in Directory.GetFiles(dir, $"{CurrentUserId}.*"))
            System.IO.File.Delete(old);

        var path = Path.Combine(dir, $"{CurrentUserId}{ext}");
        await using (var stream = System.IO.File.Create(path))
            await file.CopyToAsync(stream);

        return Ok(new { url = $"/profile-pictures/{CurrentUserId}{ext}" });
    }

    private string PicturesDirectory() =>
        Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "profile-pictures");

    private string? FindPictureUrl(int userId)
    {
        var dir = PicturesDirectory();
        if (!Directory.Exists(dir)) return null;
        var file = Directory.GetFiles(dir, $"{userId}.*").FirstOrDefault();
        return file is null ? null : $"/profile-pictures/{Path.GetFileName(file)}";
    }
}


