using System.Text.RegularExpressions;

namespace Hackathon_2025.Services;

public static partial class UsernameRules
{
    public const string Pattern = "^[a-z0-9._-]{3,24}$";

    [GeneratedRegex(Pattern, RegexOptions.CultureInvariant)]
    private static partial Regex UsernameRegex();

    public static bool IsValid(string? username) =>
        !string.IsNullOrWhiteSpace(username) && UsernameRegex().IsMatch(username.Trim());

    public static string Normalize(string username) => username.Trim().ToLowerInvariant();
}
