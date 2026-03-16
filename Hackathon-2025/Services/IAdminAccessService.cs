using System.Security.Claims;

namespace Hackathon_2025.Services;

public interface IAdminAccessService
{
    bool IsAdmin(ClaimsPrincipal principal);
    bool IsAdminEmail(string? email);
}
