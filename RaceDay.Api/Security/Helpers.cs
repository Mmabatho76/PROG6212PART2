namespace RaceDay.Api.Security;

public static class NameHelper
{
    /// <summary>
    /// Splits "Mmabatho Vilakazi" into UserName "Mmabatho" and UserSurname "Vilakazi"
    /// (the Part 1 schema stores them separately). The last word is the surname.
    /// </summary>
    public static bool TrySplit(string? fullName, out string name, out string surname)
    {
        name = surname = "";
        var trimmed = (fullName ?? "").Trim();
        var idx = trimmed.LastIndexOf(' ');
        if (idx <= 0) return false;

        name = trimmed[..idx].Trim();
        surname = trimmed[(idx + 1)..].Trim();
        return name.Length > 0 && surname.Length > 0 && name.Length <= 50 && surname.Length <= 50;
    }
}

public static class AgeRange
{
    // The Category table stores age as a VARCHAR(20) (e.g. "All Ages", "18-39"), so the API's
    // minAge / maxAge values are translated to and from that text.
    public static string Format(int? min, int? max)
    {
        if (min is null && max is null) return "All Ages";
        if (max is null) return $"{min}+";
        if (min is null) return $"Up to {max}";
        return $"{min}-{max}";
    }

    public static (int? Min, int? Max) Parse(string age)
    {
        age = (age ?? "").Trim();
        if (age.EndsWith('+') && int.TryParse(age[..^1], out var a)) return (a, null);
        if (age.StartsWith("Up to ", StringComparison.OrdinalIgnoreCase) && int.TryParse(age[6..], out var b)) return (null, b);
        if (age.StartsWith("Under ", StringComparison.OrdinalIgnoreCase) && int.TryParse(age[6..], out var u)) return (null, u - 1);
        var parts = age.Split('-');
        if (parts.Length == 2 && int.TryParse(parts[0], out var lo) && int.TryParse(parts[1], out var hi)) return (lo, hi);
        return (null, null);
    }
}
