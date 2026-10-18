using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RaceDay.Api.Data;
using RaceDay.Api.Security;

var builder = WebApplication.CreateBuilder(args);

// ---- Database (EF Core, SQL Server, code-first) ----
builder.Services.AddDbContext<RaceDayDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("RaceDayDb")));

// ---- Session-based authentication ----
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".RaceDay.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromMinutes(60);
});
builder.Services.AddSingleton<IPasswordService, PasswordService>();

builder.Services.AddControllers();

// ---- Swagger ----
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RaceDay API",
        Version = "v1",
        Description = "REST API for the RaceDay race event management system (Programming POE Part 2). " +
                      "Authentication is session based: call POST /api/auth/login first, then the browser " +
                      "keeps the session cookie so the other endpoints can be tested directly from this page."
    });

    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// Create the database from the EF model if it does not exist yet (or apply migrations if any were added).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
    if (db.Database.IsRelational() && db.Database.GetMigrations().Any())
        db.Database.Migrate();
    else
        db.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "RaceDay API v1");
    c.DocumentTitle = "RaceDay API";
});

app.UseStaticFiles();
app.UseSession();
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapControllers();

app.Run();

// Needed so the test project can use WebApplicationFactory<Program>.
public partial class Program { }
