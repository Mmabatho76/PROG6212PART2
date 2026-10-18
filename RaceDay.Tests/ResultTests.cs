using System.Net;
using System.Net.Http.Json;

namespace RaceDay.Tests;

/// <summary>Organisers capturing results and Participants viewing their own.</summary>
public class ResultTests : ApiTestBase, IClassFixture<RaceDayFactory>
{
    public ResultTests(RaceDayFactory factory) : base(factory) { }

    private static object ResultBody(int seconds = 2730, int position = 12, int total = 150) =>
        new { finishTimeSeconds = seconds, finishPosition = position, totalFinishers = total };

    private async Task<(HttpClient organiser, HttpClient participant, int eventId, int enrolmentId)> Setup()
    {
        var organiser = await LoginAs("Organiser");
        var participant = await LoginAs("Participant");
        var eventId = await CreateEvent(organiser, "Result Event");
        var categoryId = await CreateCategory(organiser, eventId, "Result Category");
        var enrolmentId = await Enrol(participant, eventId, categoryId);
        return (organiser, participant, eventId, enrolmentId);
    }

    [Fact]
    public async Task Organiser_CapturesResult_Returns201()
    {
        var (organiser, _, eventId, enrolmentId) = await Setup();

        var response = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", ResultBody());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await Json(response);
        Assert.Equal(12, body.GetProperty("finishPosition").GetInt32());
        Assert.Equal(150, body.GetProperty("totalFinishers").GetInt32());
        Assert.Equal(2730, body.GetProperty("finishTimeSeconds").GetDouble());
        Assert.Equal(eventId, body.GetProperty("eventId").GetInt32());
    }

    [Fact]
    public async Task Participant_ViewsOwnResult_InTheirRaceHistory()
    {
        var (organiser, participant, _, enrolmentId) = await Setup();
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", ResultBody(position: 3));

        var response = await participant.GetAsync("/api/results/mine");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var results = (await Json(response)).EnumerateArray().ToList();
        var result = Assert.Single(results);
        Assert.Equal("Result Event", result.GetProperty("eventName").GetString());
        Assert.Equal("Result Category", result.GetProperty("categoryName").GetString());
        Assert.Equal(3, result.GetProperty("finishPosition").GetInt32());
    }

    [Fact]
    public async Task Participant_DoesNotSeeAnotherParticipantsResults()
    {
        var (organiser, _, _, enrolmentId) = await Setup();
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", ResultBody());
        var stranger = await LoginAs("Participant");

        var results = (await Json(await stranger.GetAsync("/api/results/mine"))).EnumerateArray().ToList();
        Assert.Empty(results);
    }

    [Fact]
    public async Task CaptureResult_Twice_Returns409()
    {
        var (organiser, _, _, enrolmentId) = await Setup();
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", ResultBody());

        var again = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", ResultBody());
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_ForAnotherOrganisersEvent_Returns403()
    {
        var (_, _, _, enrolmentId) = await Setup();
        var otherOrganiser = await LoginAs("Organiser");

        var response = await otherOrganiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", ResultBody());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_ForMissingEnrolment_Returns404()
    {
        var organiser = await LoginAs("Organiser");
        var response = await organiser.PostAsJsonAsync("/api/enrolments/999999/result", ResultBody());
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_PositionGreaterThanTotal_Returns400()
    {
        var (organiser, _, _, enrolmentId) = await Setup();
        var response = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", ResultBody(position: 200, total: 150));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_InvalidTime_Returns400()
    {
        var (organiser, _, _, enrolmentId) = await Setup();
        var response = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", ResultBody(seconds: 0));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCaptureResult_Returns403()
    {
        var (_, participant, _, enrolmentId) = await Setup();
        var response = await participant.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", ResultBody());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_UpdatesResult_Returns200()
    {
        var (organiser, _, _, enrolmentId) = await Setup();
        var created = await Json(await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", ResultBody()));
        var resultId = created.GetProperty("resultId").GetInt32();

        var update = await organiser.PutAsJsonAsync($"/api/results/{resultId}", ResultBody(seconds: 2500, position: 5));

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var body = await Json(update);
        Assert.Equal(5, body.GetProperty("finishPosition").GetInt32());
        Assert.Equal(2500, body.GetProperty("finishTimeSeconds").GetDouble());
    }

    [Fact]
    public async Task Organiser_UpdatesAnotherOrganisersResult_Returns403()
    {
        var (organiser, _, _, enrolmentId) = await Setup();
        var created = await Json(await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", ResultBody()));
        var resultId = created.GetProperty("resultId").GetInt32();
        var other = await LoginAs("Organiser");

        var response = await other.PutAsJsonAsync($"/api/results/{resultId}", ResultBody());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_UpdatesMissingResult_Returns404()
    {
        var organiser = await LoginAs("Organiser");
        var response = await organiser.PutAsJsonAsync("/api/results/999999", ResultBody());
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_ListsEventResults_Returns200()
    {
        var (organiser, _, eventId, enrolmentId) = await Setup();
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", ResultBody());

        var response = await organiser.GetAsync($"/api/events/{eventId}/results");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single((await Json(response)).EnumerateArray());
    }

    [Fact]
    public async Task Organiser_ListsResultsOfAnotherOrganisersEvent_Returns403()
    {
        var (_, _, eventId, _) = await Setup();
        var other = await LoginAs("Organiser");

        var response = await other.GetAsync($"/api/events/{eventId}/results");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task EventResults_ForMissingEvent_Returns404()
    {
        var organiser = await LoginAs("Organiser");
        var response = await organiser.GetAsync("/api/events/999999/results");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
