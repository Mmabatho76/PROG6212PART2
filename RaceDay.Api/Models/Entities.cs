namespace RaceDay.Api.Models;


public class AppUser
{
    public int UserID { get; set; }
    public string UserName { get; set; } = "";
    public string UserSurname { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "";
    public string PhoneNumber { get; set; } = "";

    public List<Event> OrganisedEvents { get; set; } = new();
    public List<Enrolment> Enrolments { get; set; } = new();
}

public class EventType
{
    public int EventTypeID { get; set; }
    public string TypeName { get; set; } = "";

    public List<Event> Events { get; set; } = new();
}

public class Event
{
    public int EventID { get; set; }
    public string EventName { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime EventDate { get; set; }
    public string EventLocation { get; set; } = "";
    public double Distance { get; set; }
    public int EventTypeID { get; set; }
    public int UserID { get; set; }

    public EventType EventType { get; set; } = null!;
    public AppUser Organiser { get; set; } = null!;
    public List<Category> Categories { get; set; } = new();
    public List<Enrolment> Enrolments { get; set; } = new();
    public List<Result> Results { get; set; } = new();
}

public class Category
{
    public int CategoryID { get; set; }
    public string CategoryName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Age { get; set; } = "";
    public double Distance { get; set; }
    public int EventID { get; set; }

    public Event Event { get; set; } = null!;
    public List<Enrolment> Enrolments { get; set; } = new();
}

public class Enrolment
{
    public int EnrolmentID { get; set; }
    public DateTime EnrolmentDate { get; set; }
    public string Status { get; set; } = "";
    public int EventID { get; set; }
    public int CategoryID { get; set; }
    public int UserID { get; set; }

    public Event Event { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public AppUser User { get; set; } = null!;
    public List<Result> Results { get; set; } = new();
}

public class Result
{
    public int ResultID { get; set; }
    public TimeSpan FinishTime { get; set; }
    public int FinishPosition { get; set; }
    public DateTime PublishedDate { get; set; }
    public int TotalPosition { get; set; }
    public int EnrolmentID { get; set; }
    public int EventID { get; set; }

    public Enrolment Enrolment { get; set; } = null!;
    public Event Event { get; set; } = null!;
}


