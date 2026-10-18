using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

public class EnrolRequest
{
    /// <summary>The category (of this event) the participant wants to enter.</summary>
    /// <example>1</example>
    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }
}

/// <summary>An enrolment as seen by the participant who owns it.</summary>
public class EnrolmentResponse
{
    public int EnrolmentId { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public DateTime EventDate { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime EnrolmentDate { get; set; }

    public static readonly Expression<Func<Enrolment, EnrolmentResponse>> FromEntity = e => new EnrolmentResponse
    {
        EnrolmentId = e.EnrolmentID,
        EventId = e.EventID,
        EventName = e.Event.EventName,
        EventDate = e.Event.EventDate,
        CategoryId = e.CategoryID,
        CategoryName = e.Category.CategoryName,
        Status = e.Status,
        EnrolmentDate = e.EnrolmentDate
    };
}

/// <summary>An enrolment as seen by the organiser of the event (includes participant detail).</summary>
public class EventEnrolmentResponse
{
    public int EnrolmentId { get; set; }
    public int UserId { get; set; }
    public string ParticipantName { get; set; } = "";
    public string ParticipantEmail { get; set; } = "";
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime EnrolmentDate { get; set; }

    public static readonly Expression<Func<Enrolment, EventEnrolmentResponse>> FromEntity = e => new EventEnrolmentResponse
    {
        EnrolmentId = e.EnrolmentID,
        UserId = e.UserID,
        ParticipantName = e.User.UserName + " " + e.User.UserSurname,
        ParticipantEmail = e.User.Email,
        CategoryId = e.CategoryID,
        CategoryName = e.Category.CategoryName,
        Status = e.Status,
        EnrolmentDate = e.EnrolmentDate
    };
}

public class ResultRequest
{
    /// <summary>Finish time in seconds (e.g. 2730 = 00:45:30). Must be less than 24 hours.</summary>
    /// <example>2730</example>
    [Range(1, 86399)]
    public int FinishTimeSeconds { get; set; }

    /// <example>12</example>
    [Range(1, int.MaxValue)]
    public int FinishPosition { get; set; }

    /// <summary>Total number of finishers in the event.</summary>
    /// <example>150</example>
    [Range(1, int.MaxValue)]
    public int TotalFinishers { get; set; }
}

public class ResultResponse
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public DateTime EventDate { get; set; }
    public string CategoryName { get; set; } = "";
    public string ParticipantName { get; set; } = "";

    /// <summary>Finish time formatted hh:mm:ss.</summary>
    public TimeSpan FinishTime { get; set; }
    public double FinishTimeSeconds => FinishTime.TotalSeconds;
    public int FinishPosition { get; set; }
    public int TotalFinishers { get; set; }
    public DateTime PublishedDate { get; set; }

    public static readonly Expression<Func<Result, ResultResponse>> FromEntity = r => new ResultResponse
    {
        ResultId = r.ResultID,
        EnrolmentId = r.EnrolmentID,
        EventId = r.EventID,
        EventName = r.Event.EventName,
        EventDate = r.Event.EventDate,
        CategoryName = r.Enrolment.Category.CategoryName,
        ParticipantName = r.Enrolment.User.UserName + " " + r.Enrolment.User.UserSurname,
        FinishTime = r.FinishTime,
        FinishPosition = r.FinishPosition,
        TotalFinishers = r.TotalPosition,
        PublishedDate = r.PublishedDate
    };
}

