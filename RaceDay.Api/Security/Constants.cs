namespace RaceDay.Api.Security;

public static class SessionKeys
{
    public const string UserId = "UserId";
    public const string Role = "Role";
}

public static class RoleNames
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";

    /// <summary>Returns the canonical role name, or null if the value is not a valid role.</summary>
    public static string? Normalize(string? role)
    {
        if (string.Equals(role?.Trim(), Organiser, StringComparison.OrdinalIgnoreCase)) return Organiser;
        if (string.Equals(role?.Trim(), Participant, StringComparison.OrdinalIgnoreCase)) return Participant;
        return null;
    }
}

public static class EnrolmentStatus
{
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";
}
