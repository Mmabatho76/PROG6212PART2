using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

/// <summary>Race events. Everyone can view; only Organisers can create, update and delete their own events.</summary>
[Route("api/events")]
[Tags("3. Events")]
public class EventsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public EventsController(RaceDayDbContext db) => _db = db;

    /// <summary>List upcoming events (public). Supports optional filtering by type, location and date.</summary>
    /// <param name="type">Event type name, e.g. Run, Walk or Cycle.</param>
    /// <param name="location">Part of the location name.</param>
    /// <param name="date">Only events on this calendar date.</param>
    /// <param name="includePast">Set to true to also include events that have already taken place.</param>
    /// <response code="200">Array of events.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<EventResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvents([FromQuery] string? type, [FromQuery] string? location,
        [FromQuery] DateTime? date, [FromQuery] bool includePast = false)
    {
        var query = _db.Events.AsNoTracking().AsQueryable();

        if (date.HasValue)
        {
            var day = date.Value.Date;
            query = query.Where(e => e.EventDate >= day && e.EventDate < day.AddDays(1));
        }
        else if (!includePast)
        {
            var today = DateTime.Today;
            query = query.Where(e => e.EventDate >= today);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            var t = type.Trim().ToLower();
            query = query.Where(e => e.EventType.TypeName.ToLower() == t);
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            var l = location.Trim().ToLower();
            query = query.Where(e => e.EventLocation.ToLower().Contains(l));
        }

        var events = await query.OrderBy(e => e.EventDate).Select(EventResponse.FromEntity).ToListAsync();
        return Ok(events);
    }

    /// <summary>Get the full detail of one event, including its categories (public).</summary>
    /// <response code="200">Event detail.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvent(int id)
    {
        var ev = await _db.Events.AsNoTracking().Where(e => e.EventID == id)
            .Select(EventResponse.FromEntity).FirstOrDefaultAsync();
        if (ev is null) return NotFound(new ErrorResponse("Event not found."));

        var categories = await _db.Categories.AsNoTracking().Where(c => c.EventID == id).OrderBy(c => c.CategoryID).ToListAsync();
        ev.Categories = categories.Select(CategoryResponse.From).ToList();
        return Ok(ev);
    }

    /// <summary>Create a new event owned by the logged-in Organiser.</summary>
    /// <response code="201">New event.</response>
    /// <response code="400">Validation errors (including unknown eventTypeId).</response>
    /// <response code="401">No active session.</response>
    /// <response code="403">Logged-in user is not an Organiser.</response>
    [HttpPost]
    [SessionAuthorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateEvent([FromBody] EventRequest request)
    {
        if (!await _db.EventTypes.AnyAsync(t => t.EventTypeID == request.EventTypeId))
            return BadRequest(new ErrorResponse("eventTypeId is not valid (1 = Run, 2 = Walk, 3 = Cycle)."));

        var ev = new Event
        {
            EventName = request.Name.Trim(),
            Description = request.Description.Trim(),
            EventDate = request.EventDate!.Value,
            EventLocation = request.Location.Trim(),
            Distance = request.DistanceKm,
            EventTypeID = request.EventTypeId,
            UserID = CurrentUserId
        };
        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        var response = await _db.Events.AsNoTracking().Where(e => e.EventID == ev.EventID)
            .Select(EventResponse.FromEntity).FirstAsync();
        return CreatedAtAction(nameof(GetEvent), new { id = ev.EventID }, response);
    }

    /// <summary>Update an existing event. Only the owning Organiser may update it.</summary>
    /// <response code="200">Updated event.</response>
    /// <response code="400">Validation errors.</response>
    /// <response code="401">No active session.</response>
    /// <response code="403">Not an Organiser, or not the owning Organiser.</response>
    /// <response code="404">Event not found.</response>
    [HttpPut("{id:int}")]
    [SessionAuthorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEvent(int id, [FromBody] EventRequest request)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventID == id);
        if (ev is null) return NotFound(new ErrorResponse("Event not found."));
        if (ev.UserID != CurrentUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("You can only update your own events."));

        if (!await _db.EventTypes.AnyAsync(t => t.EventTypeID == request.EventTypeId))
            return BadRequest(new ErrorResponse("eventTypeId is not valid (1 = Run, 2 = Walk, 3 = Cycle)."));

        ev.EventName = request.Name.Trim();
        ev.Description = request.Description.Trim();
        ev.EventDate = request.EventDate!.Value;
        ev.EventLocation = request.Location.Trim();
        ev.Distance = request.DistanceKm;
        ev.EventTypeID = request.EventTypeId;
        await _db.SaveChangesAsync();

        var response = await _db.Events.AsNoTracking().Where(e => e.EventID == id)
            .Select(EventResponse.FromEntity).FirstAsync();
        return Ok(response);
    }

    /// <summary>Delete an event (and its categories, enrolments and results). Only the owning Organiser may delete it.</summary>
    /// <response code="200">Deletion confirmed.</response>
    /// <response code="401">No active session.</response>
    /// <response code="403">Not an Organiser, or not the owning Organiser.</response>
    /// <response code="404">Event not found.</response>
    [HttpDelete("{id:int}")]
    [SessionAuthorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventID == id);
        if (ev is null) return NotFound(new ErrorResponse("Event not found."));
        if (ev.UserID != CurrentUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("You can only delete your own events."));

        // Enrolment/Result foreign keys do not cascade, so remove dependants first.
        _db.Results.RemoveRange(_db.Results.Where(r => r.EventID == id));
        _db.Enrolments.RemoveRange(_db.Enrolments.Where(e => e.EventID == id));
        _db.Categories.RemoveRange(_db.Categories.Where(c => c.EventID == id));
        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();

        return Ok(new MessageResponse("Event deleted."));
    }

    /// <summary>List all events created by the logged-in Organiser, with enrolment counts (dashboard).</summary>
    /// <response code="200">Array of the organiser's events.</response>
    /// <response code="401">No active session.</response>
    /// <response code="403">Logged-in user is not an Organiser.</response>
    [HttpGet("mine")]
    [SessionAuthorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(List<EventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyEvents()
    {
        var userId = CurrentUserId;
        var events = await _db.Events.AsNoTracking()
            .Where(e => e.UserID == userId)
            .OrderBy(e => e.EventDate)
            .Select(EventResponse.FromEntity)
            .ToListAsync();

        var counts = await _db.Enrolments.AsNoTracking()
            .Where(e => e.Event.UserID == userId && e.Status != EnrolmentStatus.Cancelled)
            .GroupBy(e => e.EventID)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToListAsync();

        foreach (var ev in events)
            ev.EnrolmentCount = counts.FirstOrDefault(c => c.EventId == ev.EventId)?.Count ?? 0;

        return Ok(events);
    }
}
