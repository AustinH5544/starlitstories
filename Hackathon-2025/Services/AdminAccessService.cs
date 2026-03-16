using Hackathon_2025.Options;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Hackathon_2025.Services;

public class AdminAccessService : IAdminAccessService
{
    private readonly HashSet<string> _adminEmails;

    public AdminAccessService(IOptions<AdminOptions> options)
    {
        var configured = options.Value.Emails
            .Concat(ParseCsv(options.Value.EmailsCsv))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _adminEmails = configured;
    }

    public bool IsAdmin(ClaimsPrincipal principal)
    {
        var email =
            principal.FindFirstValue(ClaimTypes.Email) ??
            principal.FindFirstValue("email");

        return IsAdminEmail(email);
    }

    public bool IsAdminEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        return _adminEmails.Contains(email.Trim().ToLowerInvariant());
    }

    private static IEnumerable<string> ParseCsv(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Enumerable.Empty<string>();
        }

        return raw.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
