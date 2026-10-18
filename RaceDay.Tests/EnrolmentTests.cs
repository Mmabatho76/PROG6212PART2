using System.Net;
using System.Net.Http.Json;

namespace RaceDay.Tests;

/// <summary>Participants enrolling for events and the enrolment being correctly recorded.</summary>
public class EnrolmentTests : ApiTestBase, IClassFixture<RaceDayFactory>
{
    public EnrolmentTests(RaceDayFactory factory) : base(factory) { }

    [Fact]
    public async Task Participant_Enrols_Returns201_WithConfirmedStatus()
    {
        var organiser = await LoginAs("Organiser");
        var participant = await LoginAs("Participant");
        var eventId = await CreateEvent(organiser);
        var categoryId = await CreateCategory(organiser, eventId);

        var response = await participant.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await Json(response);
        Assert.Equal("Confirmed", body.GetProperty("status").GetString());
        Assert.Equal(eventId, body.GetProperty("eventId").GetInt32());
        Assert.Equal(categoryId, body.GetProperty("categoryId").GetInt32());
    }

    [Fact]
    public async Task Enrolment_IsRecordedAgainstParticipant_EventAndCategory()
    {
        var organiser = await LoginAs("Organiser");
        var participant = await LoginAs("Participant");
        var eventId = await CreateEvent(organiser, "Recorded Event");
        var categoryId = await CreateCategory(organiser, eventId, "Recorded Category");
        var enrolmentId = await Enrol(participant, eventId, categoryId);

        // Participant sees it in their own enrolments
        var mine = (await Json(await participant.GetAsync("/api/enrolments/mine"))).EnumerateArray().ToList();
        var myEnrolment = Assert.Single(mine, e => e.GetProperty("enrolmentId").GetInt32() == enrolmentId);
        Assert.Equal("Recorded Event", myEnrolment.GetProperty("eventName").GetString());
        Assert.Equal("Recorded Category", myEnrolment.GetProperty("categoryName").GetString());

        // Organiser sees the participant under their event
        var forOrganiser = await organiser.GetAsync($"/api/events/{eventId}/enrolments");
        Assert.Equal(HttpStatusCode.OK, forOrganiser.StatusCode);
        var listed = (await Json(forOrganiser)).EnumerateArray().ToList();
        var entry = Assert.Single(listed);
        Assert.Equal(enrolmentId, entry.GetProperty("enrolmentId").GetInt32());
        Assert.Equal("Recorded Category", entry.GetProperty("categoryName").GetString());
    }

    [Fact]
    public async Task Enrol_TwiceInSameEvent_Returns409()
    {
        var organiser = await LoginAs("Organiser");
        var participant = await LoginAs("Participant");
        var eventId = await CreateEvent(organiser);
        var categoryId = await CreateCategory(organiser, eventId);
        await Enrol(participant, eventId, categoryId);

        var again = await participant.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Enrol_InMissingEvent_Returns404()
    {
        var participant = await LoginAs("Participant");
        var response = await participant.PostAsJsonAsync("/api/events/999999/enrolments", new { categoryId = 1 });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_WithCategoryFromAnotherEvent_Returns404()
    {
        var organiser = await LoginAs("Organiser");
        var participant = await LoginAs("Participant");
        var eventA = await CreateEvent(organiser, "Event A");
        var eventB = await CreateEvent(organiser, "Event B");
        var categoryOfB = await CreateCategory(organiser, eventB);

        var response = await participant.PostAsJsonAsync($"/api/events/{eventA}/enrolments", new { categoryId = categoryOfB });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_Unauthenticated_Returns401()
    {
        var response = await NewClient().PostAsJsonAsync("/api/events/1/enrolments", new { categoryId = 1 });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotEnrol_Returns403()
    {
        var organiser = await LoginAs("Organiser");
        var eventId = await CreateEvent(organiser);
        var categoryId = await CreateCategory(organiser, eventId);

        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotViewEnrolmentsOfAnotherOrganisersEvent_Returns403()
    {
        var owner = await LoginAs("Organiser");
        var other = await LoginAs("Organiser");
        var eventId = await CreateEvent(owner);

        var response = await other.GetAsync($"/api/events/{eventId}/enrolments");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_ViewEnrolments_ForMissingEvent_Returns404()
    {
        var organiser = await LoginAs("Organiser");
        var response = await organiser.GetAsync("/api/events/999999/enrolments");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CancelsOwnEnrolment_Returns200_AndCanEnrolAgain()
    {
        var organiser = await LoginAs("Organiser");
        var participant = await LoginAs("Participant");
        var eventId = await CreateEvent(organiser);
        var categoryId = await CreateCategory(organiser, eventId);
        var enrolmentId = await Enrol(participant, eventId, categoryId);

        var cancel = await participant.DeleteAsync($"/api/enrolments/{enrolmentId}");
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        var mine = (await Json(await participant.GetAsync("/api/enrolments/mine"))).EnumerateArray().ToList();
        Assert.Equal("Cancelled", Assert.Single(mine).GetProperty("status").GetString());

        // a cancelled enrolment no longer blocks a new one
        var again = await participant.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId });
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Fact]
    public async Task Participant_CancelsSomeoneElsesEnrolment_Returns403()
    {
        var organiser = await LoginAs("Organiser");
        var owner = await LoginAs("Participant");
        var stranger = await LoginAs("Participant");
        var eventId = await CreateEvent(organiser);
        var categoryId = await CreateCategory(organiser, eventId);
        var enrolmentId = await Enrol(owner, eventId, categoryId);

        var response = await stranger.DeleteAsync($"/api/enrolments/{enrolmentId}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CancelsMissingEnrolment_Returns404()
    {
        var participant = await LoginAs("Participant");
        var response = await participant.DeleteAsync("/api/enrolments/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MyEnrolments_OnlyShowsTheLoggedInParticipantsEnrolments()
    {
        var organiser = await LoginAs("Organiser");
        var first = await LoginAs("Participant");
        var second = await LoginAs("Participant");
        var eventId = await CreateEvent(organiser);
        var categoryId = await CreateCategory(organiser, eventId);
        await Enrol(first, eventId, categoryId);

        var secondList = (await Json(await second.GetAsync("/api/enrolments/mine"))).EnumerateArray().ToList();
        Assert.Empty(secondList);
    }
}
