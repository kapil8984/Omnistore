using System.Security.Claims;
using OrderInventory.Domain.Entities;
using OrderInventory.Domain.Enums;

namespace OrderInventory.Application.Interfaces;

public interface ICurrentUserService
{
    int? UserId { get; }
    string? Email { get; }
    Role? Role { get; }
    bool IsAdmin { get; }
}

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
