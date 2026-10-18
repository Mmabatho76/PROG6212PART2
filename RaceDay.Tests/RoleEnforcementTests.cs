using System.Net;

namespace RaceDay.Tests;

/// <summary>
/// Proves that non-public endpoints need a session (401) and that roles are enforced (403):
/// Participants cannot use Organiser functionality and Organisers cannot use Participant functionality.
/// </summary>
public class RoleEnforcementTests : ApiTestBase, IClassFixture<RaceDayFactory>
{
    public RoleEnforcementTests(RaceDayFactory factory) : base(factory) { }

    public static IEnumerable<object[]> OrganiserOnlyEndpoints => new[]
    {
        new object[] { "POST",   "/api/events" },
        new object[] { "PUT",    "/api/events/1" },
        new object[] { "DELETE", "/api/events/1" },
        new object[] { "GET",    "/api/events/mine" },
        new object[] { "POST",   "/api/events/1/categories" },
        new object[] { "PUT",    "/api/categories/1" },
        new object[] { "DELETE", "/api/categories/1" },
        new object[] { "GET",    "/api/events/1/enrolments" },
        new object[] { "POST",   "/api/enrolments/1/result" },
        new object[] { "PUT",    "/api/results/1" },
        new object[] { "GET",    "/api/events/1/results" },
    };

    public static IEnumerable<object[]> ParticipantOnlyEndpoints => new[]
    {
        new object[] { "POST",   "/api/events/1/enrolments" },
        new object[] { "GET",    "/api/enrolments/mine" },
        new object[] { "DELETE", "/api/enrolments/1" },
        new object[] { "GET",    "/api/results/mine" },
    };

    public static IEnumerable<object[]> AnyLoggedInEndpoints => new[]
    {
        new object[] { "POST", "/api/auth/logout" },
        new object[] { "GET",  "/api/users/me" },
        new object[] { "PUT",  "/api/users/me" },
    };

    public static IEnumerable<object[]> AllProtectedEndpoints =>
        OrganiserOnlyEndpoints.Concat(ParticipantOnlyEndpoints).Concat(AnyLoggedInEndpoints);

    [Theory]
    [MemberData(nameof(AllProtectedEndpoints))]
    public async Task ProtectedEndpoint_WithoutSession_Returns401(string method, string url)
    {
        var response = await NewClient().SendAsync(Request(new HttpMethod(method), url));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(OrganiserOnlyEndpoints))]
    public async Task OrganiserEndpoint_AsParticipant_Returns403(string method, string url)
    {
        var participant = await LoginAs("Participant");
        var response = await participant.SendAsync(Request(new HttpMethod(method), url));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(ParticipantOnlyEndpoints))]
    public async Task ParticipantEndpoint_AsOrganiser_Returns403(string method, string url)
    {
        var organiser = await LoginAs("Organiser");
        var response = await organiser.SendAsync(Request(new HttpMethod(method), url));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/events")]
    [InlineData("/api/events/999999")]
    [InlineData("/api/events/1/categories")]
    public async Task PublicEndpoints_AreReachableWithoutSession(string url)
    {
        var response = await NewClient().GetAsync(url);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
