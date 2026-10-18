using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;

namespace RaceDay.Tests;

/// <summary>
/// Starts the real API in memory, but swaps SQL Server for an EF Core in-memory database
/// so the tests need no external database (and run the same locally and in GitHub Actions).
/// </summary>
public class RaceDayFactory : WebApplicationFactory<Program>
{
    public const string OrganiserCode = "TEST-ORGANISER-CODE";
    private readonly string _dbName = "RaceDayTests-" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Registration:OrganiserCode"] = OrganiserCode
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<RaceDayDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<RaceDayDbContext>(o => o.UseInMemoryDatabase(_dbName));
        });
    }
}

public abstract class ApiTestBase
{
    protected const string Password = "Passw0rd!123";
    protected readonly RaceDayFactory Factory;

    protected ApiTestBase(RaceDayFactory factory) => Factory = factory;

    protected static string NewEmail() => $"{Guid.NewGuid():N}@test.com";

    /// <summary>Each client has its own cookie container, i.e. its own session.</summary>
    protected HttpClient NewClient() => Factory.CreateClient();

    protected static object RegisterBody(string role, string email) => new
    {
        fullName = "Test User",
        email,
        password = Password,
        role,
        phoneNumber = "0821234567",
        organiserCode = role == "Organiser" ? RaceDayFactory.OrganiserCode : null
    };

    /// <summary>Registers a new user, logs them in and returns the logged-in client.</summary>
    protected async Task<HttpClient> LoginAs(string role)
    {
        var client = NewClient();
        var email = NewEmail();

        var register = await client.PostAsJsonAsync("/api/auth/register", RegisterBody(role, email));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return client;
    }

    protected static object EventBody(string name = "Test Run", int eventTypeId = 1, string location = "Pretoria") => new
    {
        name,
        description = "A test event.",
        eventDate = DateTime.Today.AddDays(30).AddHours(6),
        location,
        distanceKm = 10,
        eventTypeId
    };

    protected static object CategoryBody(string name = "Open 10km") => new
    {
        name,
        description = "Open division",
        minAge = (int?)null,
        maxAge = (int?)null,
        distanceKm = 10
    };

    protected static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    protected async Task<int> CreateEvent(HttpClient organiser, string name = "Test Run", int eventTypeId = 1, string location = "Pretoria")
    {
        var response = await organiser.PostAsJsonAsync("/api/events", EventBody(name, eventTypeId, location));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Json(response)).GetProperty("eventId").GetInt32();
    }

    protected async Task<int> CreateCategory(HttpClient organiser, int eventId, string name = "Open 10km")
    {
        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", CategoryBody(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Json(response)).GetProperty("categoryId").GetInt32();
    }

    protected async Task<int> Enrol(HttpClient participant, int eventId, int categoryId)
    {
        var response = await participant.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Json(response)).GetProperty("enrolmentId").GetInt32();
    }

    protected static HttpRequestMessage Request(HttpMethod method, string url) =>
        new(method, url) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
}
