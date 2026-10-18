using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

/// <summary>Race results. Organisers capture them for their events; Participants view their own.</summary>
[Tags("6. Results")]
public class ResultsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public ResultsController(RaceDayDbContext db) => _db = db;

    /// <summary>Capture and publish a finish time and position for an enrolled Participant.</summary>
    /// <remarks>Body: { "finishTimeSeconds": 2730, "finishPosition": 12, "totalFinishers": 150 }.</remarks>
    /// <response code="201">Result record.</response>
    /// <response code="400">Validation errors (e.g. position greater than total finishers, or cancelled enrolment).</response>
    /// <response code="401">No active session.</response>
    /// <response code="403">Not an Organiser, or not the Organiser who owns the event.</response>
    /// <response code="404">Enrolment does not exist.</response>
    /// <response code="409">A result has already been captured for this enrolment.</response>
    [HttpPost("/api/enrolments/{enrolmentId:int}/result")]
    [SessionAuthorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CaptureResult(int enrolmentId, [FromBody] ResultRequest request)
    {
        var enrolment = await _db.Enrolments.AsNoTracking().Include(e => e.Event)
            .FirstOrDefaultAsync(e => e.EnrolmentID == enrolmentId);
        if (enrolment is null) return NotFound(new ErrorResponse("Enrolment not found."));
        if (enrolment.Event.UserID != CurrentUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("You can only capture results for your own events."));
        if (await _db.Results.AnyAsync(r => r.EnrolmentID == enrolmentId))
            return Conflict(new ErrorResponse("A result has already been captured for this enrolment."));
        if (enrolment.Status == EnrolmentStatus.Cancelled)
            return BadRequest(new ErrorResponse("Cannot capture a result for a cancelled enrolment."));
        if (request.FinishPosition > request.TotalFinishers)
            return BadRequest(new ErrorResponse("finishPosition cannot be greater than totalFinishers."));

        var result = new Result
        {
            FinishTime = TimeSpan.FromSeconds(request.FinishTimeSeconds),
            FinishPosition = request.FinishPosition,
            TotalPosition = request.TotalFinishers,
            PublishedDate = DateTime.UtcNow,
            EnrolmentID = enrolmentId,
            EventID = enrolment.EventID
        };
        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        var response = await _db.Results.AsNoTracking().Where(r => r.ResultID == result.ResultID)
            .Select(ResultResponse.FromEntity).FirstAsync();
        return Created($"/api/events/{enrolment.EventID}/results", response);
    }

    /// <summary>Update a previously captured result.</summary>
    /// <response code="200">Updated result.</response>
    /// <response code="400">Validation errors.</response>
    /// <response code="401">No active session.</response>
    /// <response code="403">Not an Organiser, or not the owning Organiser.</response>
    /// <response code="404">Result not found.</response>
    [HttpPut("/api/results/{id:int}")]
    [SessionAuthorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateResult(int id, [FromBody] ResultRequest request)
    {
        var result = await _db.Results.Include(r => r.Event).FirstOrDefaultAsync(r => r.ResultID == id);
        if (result is null) return NotFound(new ErrorResponse("Result not found."));
        if (result.Event.UserID != CurrentUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("You can only update results for your own events."));
        if (request.FinishPosition > request.TotalFinishers)
            return BadRequest(new ErrorResponse("finishPosition cannot be greater than totalFinishers."));

        result.FinishTime = TimeSpan.FromSeconds(request.FinishTimeSeconds);
        result.FinishPosition = request.FinishPosition;
        result.TotalPosition = request.TotalFinishers;
        result.PublishedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var response = await _db.Results.AsNoTracking().Where(r => r.ResultID == id)
            .Select(ResultResponse.FromEntity).FirstAsync();
        return Ok(response);
    }

    /// <summary>Get the logged-in Participant's personal race history across all events.</summary>
    /// <response code="200">Array of results with event name, date, category, time and position.</response>
    /// <response code="401">No active session.</response>
    /// <response code="403">Logged-in user is not a Participant.</response>
    [HttpGet("/api/results/mine")]
    [SessionAuthorize(Roles = RoleNames.Participant)]
    [ProducesResponseType(typeof(List<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyResults()
    {
        var userId = CurrentUserId;
        var results = await _db.Results.AsNoTracking()
            .Where(r => r.Enrolment.UserID == userId)
            .OrderByDescending(r => r.Event.EventDate)
            .Select(ResultResponse.FromEntity)
            .ToListAsync();
        return Ok(results);
    }

    /// <summary>List all published results for an event owned by the logged-in Organiser.</summary>
    /// <response code="200">Array of results.</response>
    /// <response code="401">No active session.</response>
    /// <response code="403">Not an Organiser, or not the owning Organiser.</response>
    /// <response code="404">Event not found.</response>
    [HttpGet("/api/events/{eventId:int}/results")]
    [SessionAuthorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(List<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEventResults(int eventId)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventID == eventId);
        if (ev is null) return NotFound(new ErrorResponse("Event not found."));
        if (ev.UserID != CurrentUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("You can only view results for your own events."));

        var results = await _db.Results.AsNoTracking()
            .Where(r => r.EventID == eventId)
            .OrderBy(r => r.FinishPosition)
            .Select(ResultResponse.FromEntity)
            .ToListAsync();
        return Ok(results);
    }
}

