using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace DarkVelocity.E2E.Auth;

public static class TestTokenGenerator
{
    private const string SecretKey = "change-this-in-production-min-32-chars!!";
    private const string Issuer = "https://api.darkvelocity.app";
    private const string Audience = "darkvelocity-apps";

    public static string GenerateToken(
        Guid userId,
        Guid orgId,
        Guid? siteId = null,
        Guid? sessionId = null,
        string displayName = "E2E Test User",
        IEnumerable<string>? roles = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Name, displayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("org_id", orgId.ToString()),
        };

        if (siteId.HasValue)
            claims.Add(new Claim("site_id", siteId.Value.ToString()));

        if (sessionId.HasValue)
            claims.Add(new Claim("session_id", sessionId.Value.ToString()));

        if (roles is not null)
        {
            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
