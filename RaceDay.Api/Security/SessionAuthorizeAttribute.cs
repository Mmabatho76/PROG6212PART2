using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Security;

/// <summary>
/// Session-based authorisation. Reads the user id and role that were stored in the server-side
/// session at login. No session -> 401 Unauthorized. Wrong role -> 403 Forbidden.
/// Leave <see cref="Roles"/> empty to allow any authenticated user.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SessionAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
{
    /// <summary>Comma separated list of allowed roles.</summary>
    public string? Roles { get; set; }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;
        await session.LoadAsync(context.HttpContext.RequestAborted);

        var userId = session.GetInt32(SessionKeys.UserId);
        var role = session.GetString(SessionKeys.Role);

        if (userId is null || role is null)
        {
            context.Result = new ObjectResult(new ErrorResponse("You must be logged in to access this resource."))
            { StatusCode = StatusCodes.Status401Unauthorized };
            return;
        }

        if (!string.IsNullOrWhiteSpace(Roles))
        {
            var allowed = Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (!allowed.Contains(role, StringComparer.OrdinalIgnoreCase))
            {
                context.Result = new ObjectResult(new ErrorResponse($"This action is restricted to: {string.Join(", ", allowed)}."))
                { StatusCode = StatusCodes.Status403Forbidden };
            }
        }
    }
}
