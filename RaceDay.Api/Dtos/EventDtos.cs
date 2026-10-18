using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Text.Json.Serialization;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Dtos;

public class EventRequest
{
    /// <example>Soweto Fun Run</example>
    [Required, StringLength(100)]
    public string Name { get; set; } = "";

    /// <example>A scenic 10km run through Soweto.</example>
    [Required, StringLength(500)]
    public string Description { get; set; } = "";

    /// <example>2026-12-05T06:00:00</example>
    [Required]
    public DateTime? EventDate { get; set; }

    /// <example>Soweto</example>
    [Required, StringLength(100)]
    public string Location { get; set; } = "";

    /// <example>10</example>
    [Range(0.1, 1000)]
    public double DistanceKm { get; set; }

    /// <summary>1 = Run, 2 = Walk, 3 = Cycle.</summary>
    /// <example>1</example>
    [Range(1, int.MaxValue)]
    public int EventTypeId { get; set; }
}

public class EventResponse
{
    public int EventId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = "";
    public double DistanceKm { get; set; }
    public int EventTypeId { get; set; }
    public string EventType { get; set; } = "";
    public int OrganiserId { get; set; }
    public string OrganiserName { get; set; } = "";

    /// <summary>Only filled in for "my events" (Organiser dashboard).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? EnrolmentCount { get; set; }

    /// <summary>Only filled in for the single-event detail view.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<CategoryResponse>? Categories { get; set; }

    public static readonly Expression<Func<Event, EventResponse>> FromEntity = e => new EventResponse
    {
        EventId = e.EventID,
        Name = e.EventName,
        Description = e.Description,
        EventDate = e.EventDate,
        Location = e.EventLocation,
        DistanceKm = e.Distance,
        EventTypeId = e.EventTypeID,
        EventType = e.EventType.TypeName,
        OrganiserId = e.UserID,
        OrganiserName = e.Organiser.UserName + " " + e.Organiser.UserSurname
    };
}

public class CategoryRequest : IValidatableObject
{
    /// <example>Under 20</example>
    [Required, StringLength(50)]
    public string Name { get; set; } = "";

    /// <example>Junior division for runners under 20.</example>
    [Required, StringLength(200)]
    public string Description { get; set; } = "";

    /// <summary>Leave empty for no lower age limit.</summary>
    [Range(0, 120)]
    public int? MinAge { get; set; }

    /// <summary>Leave empty for no upper age limit.</summary>
    [Range(0, 120)]
    public int? MaxAge { get; set; }

    /// <example>10</example>
    [Range(0.1, 1000)]
    public double DistanceKm { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinAge.HasValue && MaxAge.HasValue && MinAge > MaxAge)
            yield return new ValidationResult("minAge cannot be greater than maxAge.", new[] { nameof(MinAge), nameof(MaxAge) });
    }
}

public class CategoryResponse
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public string AgeRange { get; set; } = "";
    public double DistanceKm { get; set; }

    public static CategoryResponse From(Category c)
    {
        var (min, max) = Security.AgeRange.Parse(c.Age);
        return new CategoryResponse
        {
            CategoryId = c.CategoryID,
            EventId = c.EventID,
            Name = c.CategoryName,
            Description = c.Description,
            MinAge = min,
            MaxAge = max,
            AgeRange = c.Age,
            DistanceKm = c.Distance
        };
    }
}

