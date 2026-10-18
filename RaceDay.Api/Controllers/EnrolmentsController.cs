using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

/// <summary>Participants enrol into events under a chosen category; Organisers view enrolments for their events.</summary>
[Tags("5. Event Enrolments")]
public class EnrolmentsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public EnrolmentsController(RaceDayDbContext db) => _db = db;

    /// <summary>Enrol the logged-in Participant into an event under a chosen category.</summary>
    /// <remarks>Body: { "categoryId": 1 }. The link between the participant, the event and the category is recorded.</remarks>
    /// <response code="201">Enrolment record with status.</response>
    /// <response code="400">Validation errors.</response>
    /// <response code="401">No active session.</response>
    /// <response code="403">Logged-in user is not a Participant.</response>
    /// <response code="404">Event not found, or the category does not belong to that event.</response>
    /// <response code="409">Already enrolled in this event.</response>
    [HttpPost("/api/events/{eventId:int}/enrolments")]
    [SessionAuthorize(Roles = RoleNames.Participant)]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Enrol(int eventId, [FromBody] EnrolRequest request)
    {
        if (!await _db.Events.AnyAsync(e => e.EventID == eventId))
            return NotFound(new ErrorResponse("Event not found."));

        if (!await _db.Categories.AnyAsync(c => c.CategoryID == request.CategoryId && c.EventID == eventId))
            return NotFound(new ErrorResponse("Category not found for this event."));

        var userId = CurrentUserId;
        if (await _db.Enrolments.AnyAsync(e => e.EventID == eventId && e.UserID == userId && e.Status != EnrolmentStatus.Cancelled))
            return Conflict(new ErrorResponse("You are already enrolled in this event."));

        var enrolment = new Enrolment
        {
            EnrolmentDate = DateTime.UtcNow,
            Status = EnrolmentStatus.Confirmed,
            EventID = eventId,
            CategoryID = request.CategoryId,
            UserID = userId
        };
        _db.Enrolments.Add(enrolment);
        await _db.SaveChangesAsync();

        var response = await _db.Enrolments.AsNoTracking().Where(e => e.EnrolmentID == enrolment.EnrolmentID)
            .Select(EnrolmentResponse.FromEntity).FirstAsync();
        return Created("/api/enrolments/mine", response);
    }

    /// <summary>List the events the logged-in Participant has enrolled for, with status.</summary>
    /// <response code="200">Array of enrolments.</response>
    /// <response code="401">No active session.</response>
    /// <response code="403">Logged-in user is not a Participant.</response>
    [HttpGet("/api/enrolments/mine")]
    [SessionAuthorize(Roles = RoleNames.Participant)]
    [ProducesResponseType(typeof(List<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyEnrolments()
    {
        var userId = CurrentUserId;
        var list = await _db.Enrolments.AsNoTracking()
            .Where(e => e.UserID == userId)
            .OrderByDescending(e => e.EnrolmentDate)
            .Select(EnrolmentResponse.FromEntity)
            .ToListAsync();
        return Ok(list);
    }

    /// <summary>List all Participants enrolled in an event owned by the logged-in Organiser.</summary>
    /// <response code="200">Array of enrolments with participant and category detail.</response>
    /// <response code="401">No active session.</response>
    /// <response code="403">Not an Organiser, or not the owning Organiser.</response>
    /// <response code="404">Event not found.</response>
    [HttpGet("/api/events/{eventId:int}/enrolments")]
    [SessionAuthorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(List<EventEnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEventEnrolments(int eventId)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventID == eventId);
        if (ev is null) return NotFound(new ErrorResponse("Event not found."));
        if (ev.UserID != CurrentUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("You can only view enrolments for your own events."));

        var list = await _db.Enrolments.AsNoTracking()
            .Where(e => e.EventID == eventId)
            .OrderBy(e => e.EnrolmentDate)
            .Select(EventEnrolmentResponse.FromEntity)
            .ToListAsync();
        return Ok(list);
    }

    /// <summary>Cancel the logged-in Participant's own enrolment.</summary>
    /// <response code="200">Cancellation confirmed (status becomes Cancelled).</response>
    /// <response code="401">No active session.</response>
    /// <response code="403">Not a Participant, or the enrolment belongs to someone else.</response>
    /// <response code="404">Enrolment not found.</response>
    /// <response code="409">Already cancelled, or a result has already been published.</response>
    [HttpDelete("/api/enrolments/{id:int}")]
    [SessionAuthorize(Roles = RoleNames.Participant)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelEnrolment(int id)
    {
        var enrolment = await _db.Enrolments.FirstOrDefaultAsync(e => e.EnrolmentID == id);
        if (enrolment is null) return NotFound(new ErrorResponse("Enrolment not found."));
        if (enrolment.UserID != CurrentUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("You can only cancel your own enrolment."));
        if (enrolment.Status == EnrolmentStatus.Cancelled)
            return Conflict(new ErrorResponse("This enrolment is already cancelled."));
        if (await _db.Results.AnyAsync(r => r.EnrolmentID == id))
            return Conflict(new ErrorResponse("A result has already been published for this enrolment."));

        enrolment.Status = EnrolmentStatus.Cancelled;
        await _db.SaveChangesAsync();
        return Ok(new MessageResponse("Enrolment cancelled."));
    }
}

