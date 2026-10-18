using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Id of the logged-in user, read from the server-side session.</summary>
    protected int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;

    /// <summary>Role of the logged-in user, read from the server-side session.</summary>
    protected string? CurrentRole => HttpContext.Session.GetString(SessionKeys.Role);
}
