using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;

namespace RaceDay.Tests;

/// <summary>User registration, login, logout, session handling and the profile endpoints.</summary>
public class AuthTests : ApiTestBase, IClassFixture<RaceDayFactory>
{
    public AuthTests(RaceDayFactory factory) : base(factory) { }

    [Fact]
    public async Task Register_Participant_Returns201_WithoutPassword()
    {
        var client = NewClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", RegisterBody("Participant", NewEmail()));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await Json(response);
        Assert.Equal("Participant", body.GetProperty("role").GetString());
        Assert.False(body.TryGetProperty("password", out _));
        Assert.False(body.TryGetProperty("passwordHash", out _));
    }

    [Fact]
    public async Task Register_Organiser_WithValidCode_Returns201()
    {
        var response = await NewClient().PostAsJsonAsync("/api/auth/register", RegisterBody("Organiser", NewEmail()));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Organiser", (await Json(response)).GetProperty("role").GetString());
    }

    [Fact]
    public async Task Register_Organiser_WithoutCode_Returns403()
    {
        var response = await NewClient().PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Sneaky User", email = NewEmail(), password = Password, role = "Organiser", phoneNumber = "0821234567"
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var client = NewClient();
        var email = NewEmail();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register", RegisterBody("Participant", email))).StatusCode);

        var duplicate = await client.PostAsJsonAsync("/api/auth/register", RegisterBody("Participant", email));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Register_ShortPassword_Returns400()
    {
        var response = await NewClient().PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Test User", email = NewEmail(), password = "short", role = "Participant", phoneNumber = "0821234567"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidRole_Returns400()
    {
        var response = await NewClient().PostAsJsonAsync("/api/auth/register", RegisterBody("Admin", NewEmail()));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_SingleWordName_Returns400()
    {
        var response = await NewClient().PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Madonna", email = NewEmail(), password = Password, role = "Participant", phoneNumber = "0821234567"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_StoresPasswordHashed_NotPlainText()
    {
        var email = NewEmail();
        await NewClient().PostAsJsonAsync("/api/auth/register", RegisterBody("Participant", email));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var user = await db.AppUsers.AsNoTracking().FirstAsync(u => u.Email == email);

        Assert.NotEqual(Password, user.PasswordHash);
        Assert.DoesNotContain(Password, user.PasswordHash);
        Assert.StartsWith("PBKDF2$", user.PasswordHash);
    }

    [Fact]
    public async Task Login_ValidCredentials_Returns200_AndStartsSession()
    {
        var client = NewClient();
        var email = NewEmail();
        await client.PostAsJsonAsync("/api/auth/register", RegisterBody("Participant", email));

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal("Participant", (await Json(login)).GetProperty("role").GetString());

        // the session cookie keeps the user logged in for later requests
        var me = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(email, (await Json(me)).GetProperty("email").GetString());
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var client = NewClient();
        var email = NewEmail();
        await client.PostAsJsonAsync("/api/auth/register", RegisterBody("Participant", email));

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPassword1" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var login = await NewClient().PostAsJsonAsync("/api/auth/login", new { email = NewEmail(), password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsSession()
    {
        var client = await LoginAs("Participant");

        var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);

        var me = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutSession_Returns401()
    {
        var logout = await NewClient().PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.Unauthorized, logout.StatusCode);
    }

    [Fact]
    public async Task GetProfile_Unauthenticated_Returns401()
    {
        var response = await NewClient().GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_ChangesNameAndPhone()
    {
        var client = await LoginAs("Participant");

        var update = await client.PutAsJsonAsync("/api/users/me", new { fullName = "Updated Person", phoneNumber = "0831112222" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var me = await Json(await client.GetAsync("/api/users/me"));
        Assert.Equal("Updated Person", me.GetProperty("fullName").GetString());
        Assert.Equal("0831112222", me.GetProperty("phoneNumber").GetString());
    }

    [Fact]
    public async Task UpdateProfile_Unauthenticated_Returns401()
    {
        var response = await NewClient().PutAsJsonAsync("/api/users/me", new { fullName = "Updated Person", phoneNumber = "0831112222" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_InvalidName_Returns400()
    {
        var client = await LoginAs("Participant");
        var response = await client.PutAsJsonAsync("/api/users/me", new { fullName = "Mononym", phoneNumber = "0831112222" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
