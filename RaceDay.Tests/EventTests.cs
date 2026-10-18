using System.Net;
using System.Net.Http.Json;

namespace RaceDay.Tests;

/// <summary>Event management by Organisers, public viewing, and category management.</summary>
public class EventTests : ApiTestBase, IClassFixture<RaceDayFactory>
{
    public EventTests(RaceDayFactory factory) : base(factory) { }

    [Fact]
    public async Task GetEvents_IsPublic_Returns200()
    {
        var response = await NewClient().GetAsync("/api/events");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CreatesEvent_Returns201_AndEventIsVisibleToEveryone()
    {
        var organiser = await LoginAs("Organiser");
        var id = await CreateEvent(organiser, "Public Visible Run");

        var anonymous = await NewClient().GetAsync($"/api/events/{id}");
        Assert.Equal(HttpStatusCode.OK, anonymous.StatusCode);
        var body = await Json(anonymous);
        Assert.Equal("Public Visible Run", body.GetProperty("name").GetString());
        Assert.Equal("Run", body.GetProperty("eventType").GetString());
    }

    [Fact]
    public async Task Participant_CreatesEvent_Returns403()
    {
        var participant = await LoginAs("Participant");
        var response = await participant.PostAsJsonAsync("/api/events", EventBody());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_CreatesEvent_Returns401()
    {
        var response = await NewClient().PostAsJsonAsync("/api/events", EventBody());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_UnknownEventType_Returns400()
    {
        var organiser = await LoginAs("Organiser");
        var response = await organiser.PostAsJsonAsync("/api/events", EventBody(eventTypeId: 99));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_MissingName_Returns400()
    {
        var organiser = await LoginAs("Organiser");
        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            name = "", description = "x", eventDate = DateTime.Today.AddDays(5), location = "Pretoria", distanceKm = 5, eventTypeId = 1
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetEvent_NotFound_Returns404()
    {
        var response = await NewClient().GetAsync("/api/events/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_UpdatesOwnEvent_Returns200()
    {
        var organiser = await LoginAs("Organiser");
        var id = await CreateEvent(organiser, "Before");

        var update = await organiser.PutAsJsonAsync($"/api/events/{id}", EventBody("After", eventTypeId: 3));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var body = await Json(await NewClient().GetAsync($"/api/events/{id}"));
        Assert.Equal("After", body.GetProperty("name").GetString());
        Assert.Equal("Cycle", body.GetProperty("eventType").GetString());
    }

    [Fact]
    public async Task Organiser_UpdatesAnotherOrganisersEvent_Returns403()
    {
        var owner = await LoginAs("Organiser");
        var other = await LoginAs("Organiser");
        var id = await CreateEvent(owner);

        var response = await other.PutAsJsonAsync($"/api/events/{id}", EventBody("Hijacked"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_UpdatesMissingEvent_Returns404()
    {
        var organiser = await LoginAs("Organiser");
        var response = await organiser.PutAsJsonAsync("/api/events/999999", EventBody());
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_DeletesOwnEvent_Returns200_ThenEventIsGone()
    {
        var organiser = await LoginAs("Organiser");
        var id = await CreateEvent(organiser);

        var delete = await organiser.DeleteAsync($"/api/events/{id}");
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        var get = await NewClient().GetAsync($"/api/events/{id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Organiser_DeletesEventWithEnrolments_Returns200()
    {
        var organiser = await LoginAs("Organiser");
        var participant = await LoginAs("Participant");
        var eventId = await CreateEvent(organiser);
        var categoryId = await CreateCategory(organiser, eventId);
        await Enrol(participant, eventId, categoryId);

        var delete = await organiser.DeleteAsync($"/api/events/{eventId}");
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
    }

    [Fact]
    public async Task Organiser_DeletesAnotherOrganisersEvent_Returns403()
    {
        var owner = await LoginAs("Organiser");
        var other = await LoginAs("Organiser");
        var id = await CreateEvent(owner);

        var response = await other.DeleteAsync($"/api/events/{id}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_DeletesEvent_Returns403()
    {
        var organiser = await LoginAs("Organiser");
        var participant = await LoginAs("Participant");
        var id = await CreateEvent(organiser);

        var response = await participant.DeleteAsync($"/api/events/{id}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MyEvents_ReturnsOnlyTheLoggedInOrganisersEvents()
    {
        var organiser = await LoginAs("Organiser");
        var other = await LoginAs("Organiser");
        await CreateEvent(organiser, "Mine 1");
        await CreateEvent(organiser, "Mine 2");
        await CreateEvent(other, "Not Mine");

        var response = await organiser.GetAsync("/api/events/mine");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var names = (await Json(response)).EnumerateArray().Select(e => e.GetProperty("name").GetString()).ToList();
        Assert.Contains("Mine 1", names);
        Assert.Contains("Mine 2", names);
        Assert.DoesNotContain("Not Mine", names);
    }

    [Fact]
    public async Task GetEvents_CanFilterByTypeAndLocation()
    {
        var organiser = await LoginAs("Organiser");
        var location = "Filterville" + Guid.NewGuid().ToString("N")[..6];
        await CreateEvent(organiser, "Filter Cycle", eventTypeId: 3, location: location);
        await CreateEvent(organiser, "Filter Run", eventTypeId: 1, location: location);

        var response = await NewClient().GetAsync($"/api/events?type=cycle&location={location}");
        var events = (await Json(response)).EnumerateArray().ToList();

        Assert.Single(events);
        Assert.Equal("Filter Cycle", events[0].GetProperty("name").GetString());
    }

    // ---------------- Categories ----------------

    [Fact]
    public async Task Organiser_AddsCategory_Returns201_AndEveryoneCanViewIt()
    {
        var organiser = await LoginAs("Organiser");
        var eventId = await CreateEvent(organiser);

        var create = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", new
        {
            name = "Under 20", description = "Junior division", minAge = (int?)null, maxAge = 19, distanceKm = 10
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var list = await NewClient().GetAsync($"/api/events/{eventId}/categories");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var categories = (await Json(list)).EnumerateArray().ToList();
        Assert.Single(categories);
        Assert.Equal("Under 20", categories[0].GetProperty("name").GetString());
        Assert.Equal(19, categories[0].GetProperty("maxAge").GetInt32());
    }

    [Fact]
    public async Task Participant_AddsCategory_Returns403()
    {
        var organiser = await LoginAs("Organiser");
        var participant = await LoginAs("Participant");
        var eventId = await CreateEvent(organiser);

        var response = await participant.PostAsJsonAsync($"/api/events/{eventId}/categories", CategoryBody());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_AddsCategoryToAnotherOrganisersEvent_Returns403()
    {
        var owner = await LoginAs("Organiser");
        var other = await LoginAs("Organiser");
        var eventId = await CreateEvent(owner);

        var response = await other.PostAsJsonAsync($"/api/events/{eventId}/categories", CategoryBody());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetCategories_ForMissingEvent_Returns404()
    {
        var response = await NewClient().GetAsync("/api/events/999999/categories");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddCategory_MinAgeGreaterThanMaxAge_Returns400()
    {
        var organiser = await LoginAs("Organiser");
        var eventId = await CreateEvent(organiser);

        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", new
        {
            name = "Broken", description = "Invalid ages", minAge = 40, maxAge = 20, distanceKm = 10
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_UpdatesAndDeletesOwnCategory()
    {
        var organiser = await LoginAs("Organiser");
        var eventId = await CreateEvent(organiser);
        var categoryId = await CreateCategory(organiser, eventId, "Old name");

        var update = await organiser.PutAsJsonAsync($"/api/categories/{categoryId}", new
        {
            name = "Senior", description = "Senior division", minAge = 60, maxAge = (int?)null, distanceKm = 21
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("Senior", (await Json(update)).GetProperty("name").GetString());

        var delete = await organiser.DeleteAsync($"/api/categories/{categoryId}");
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_WithActiveEnrolment_Returns409()
    {
        var organiser = await LoginAs("Organiser");
        var participant = await LoginAs("Participant");
        var eventId = await CreateEvent(organiser);
        var categoryId = await CreateCategory(organiser, eventId);
        await Enrol(participant, eventId, categoryId);

        var delete = await organiser.DeleteAsync($"/api/categories/{categoryId}");
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_OfAnotherOrganiser_Returns403()
    {
        var owner = await LoginAs("Organiser");
        var other = await LoginAs("Organiser");
        var eventId = await CreateEvent(owner);
        var categoryId = await CreateCategory(owner, eventId);

        var delete = await other.DeleteAsync($"/api/categories/{categoryId}");
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }
}
