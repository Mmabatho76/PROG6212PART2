using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

/// <summary>Age / distance categories for events. Everyone can view; the owning Organiser manages them.</summary>
[Tags("4. Categories")]
public class CategoriesController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public CategoriesController(RaceDayDbContext db) => _db = db;

    /// <summary>List all categories for an event (public).</summary>
    /// <response code="200">Array of categories.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("/api/events/{eventId:int}/categories")]
    [ProducesResponseType(typeof(List<CategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategories(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventID == eventId))
            return NotFound(new ErrorResponse("Event not found."));

        var categories = await _db.Categories.AsNoTracking()
            .Where(c => c.EventID == eventId).OrderBy(c => c.CategoryID).ToListAsync();
        return Ok(categories.Select(CategoryResponse.From).ToList());
    }

    /// <summary>Add an age or distance category to an event owned by the logged-in Organiser.</summary>
    /// <response code="201">New category.</response>
    /// <response code="400">Validation errors (e.g. minAge greater than maxAge).</response>
    /// <response code="401">No active session.</response>
    /// <response code="403">Not an Organiser, or not the owning Organiser.</response>
    /// <response code="404">Event not found.</response>
    [HttpPost("/api/events/{eventId:int}/categories")]
    [SessionAuthorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateCategory(int eventId, [FromBody] CategoryRequest request)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventID == eventId);
        if (ev is null) return NotFound(new ErrorResponse("Event not found."));
        if (ev.UserID != CurrentUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("You can only add categories to your own events."));

        var category = new Category
        {
            CategoryName = request.Name.Trim(),
            Description = request.Description.Trim(),
            Age = AgeRange.Format(request.MinAge, request.MaxAge),
            Distance = request.DistanceKm,
            EventID = eventId
        };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return Created($"/api/events/{eventId}/categories", CategoryResponse.From(category));
    }

    /// <summary>Update an existing category.</summary>
    /// <response code="200">Updated category.</response>
    /// <response code="400">Validation errors.</response>
    /// <response code="401">No active session.</response>
    /// <response code="403">Not an Organiser, or the category belongs to another Organiser's event.</response>
    /// <response code="404">Category not found.</response>
    [HttpPut("/api/categories/{id:int}")]
    [SessionAuthorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] CategoryRequest request)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryID == id);
        if (category is null) return NotFound(new ErrorResponse("Category not found."));
        if (category.Event.UserID != CurrentUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("You can only update categories of your own events."));

        category.CategoryName = request.Name.Trim();
        category.Description = request.Description.Trim();
        category.Age = AgeRange.Format(request.MinAge, request.MaxAge);
        category.Distance = request.DistanceKm;
        await _db.SaveChangesAsync();

        return Ok(CategoryResponse.From(category));
    }

    /// <summary>Delete a category from an event owned by the logged-in Organiser.</summary>
    /// <response code="200">Deletion confirmed.</response>
    /// <response code="401">No active session.</response>
    /// <response code="403">Not an Organiser, or the category belongs to another Organiser's event.</response>
    /// <response code="404">Category not found.</response>
    /// <response code="409">The category still has active enrolments.</response>
    [HttpDelete("/api/categories/{id:int}")]
    [SessionAuthorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryID == id);
        if (category is null) return NotFound(new ErrorResponse("Category not found."));
        if (category.Event.UserID != CurrentUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("You can only delete categories of your own events."));

        var hasActive = await _db.Enrolments.AnyAsync(e => e.CategoryID == id && e.Status != EnrolmentStatus.Cancelled);
        if (hasActive)
            return Conflict(new ErrorResponse("This category has active enrolments and cannot be deleted."));

        // Cancelled enrolments still reference the category (no cascade), so remove them first.
        var cancelled = _db.Enrolments.Where(e => e.CategoryID == id);
        _db.Results.RemoveRange(_db.Results.Where(r => cancelled.Select(e => e.EnrolmentID).Contains(r.EnrolmentID)));
        _db.Enrolments.RemoveRange(cancelled);
        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();

        return Ok(new MessageResponse("Category deleted."));
    }
}



