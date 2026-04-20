namespace LeaseLense.Application.Common;

public static class AnonymizedNameGenerator
{
    private static readonly string[] Prefixes =
    [
        "Anonymous",
        "Quiet",
        "Urban",
        "Candid",
        "Neutral",
        "Hidden",
        "Fair",
        "Steady",
        "Calm",
        "Civic"
    ];

    private static readonly string[] Suffixes =
    [
        "Renter",
        "Resident",
        "Neighbor",
        "Voice",
        "Tenant",
        "Reviewer",
        "Member",
        "Dweller",
        "Guide",
        "Scout"
    ];

    public static string Generate(Guid seed)
    {
        var bytes = seed.ToByteArray();
        var prefix = Prefixes[bytes[0] % Prefixes.Length];
        var suffix = Suffixes[bytes[1] % Suffixes.Length];
        return $"{prefix} {suffix}";
    }
}
