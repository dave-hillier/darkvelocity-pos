using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DarkVelocity.Auth.Api.Data;
using DarkVelocity.Auth.Api.Dtos;
using DarkVelocity.Auth.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace DarkVelocity.Auth.Api.Services;

public class AuthService : IAuthService
{
    private readonly AuthDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthService(AuthDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<LoginResponse?> LoginWithPinAsync(string pin, Guid? locationId)
    {
        var users = await _context.PosUsers
            .Include(u => u.UserGroup)
            .Include(u => u.HomeLocation)
            .Where(u => u.IsActive)
            .ToListAsync();

        var user = users.FirstOrDefault(u => VerifyPin(pin, u.PinHash));

        if (user == null)
            return null;

        return await GenerateLoginResponse(user, locationId ?? user.HomeLocationId);
    }

    public async Task<LoginResponse?> LoginWithQrAsync(string token, Guid? locationId)
    {
        var user = await _context.PosUsers
            .Include(u => u.UserGroup)
            .Include(u => u.HomeLocation)
            .FirstOrDefaultAsync(u => u.QrCodeToken == token && u.IsActive);

        if (user == null)
            return null;

        return await GenerateLoginResponse(user, locationId ?? user.HomeLocationId);
    }

    public async Task<LoginResponse?> RefreshTokenAsync(string refreshToken)
    {
        // In a real implementation, validate the refresh token
        // For now, decode and re-issue
        var handler = new JwtSecurityTokenHandler();
        try
        {
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? "default-dev-key-minimum-32-chars!");
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"] ?? "DarkVelocity",
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"] ?? "DarkVelocity",
                ValidateLifetime = false // Allow expired tokens for refresh
            };

            var principal = handler.ValidateToken(refreshToken, validationParameters, out _);
            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return null;

            var user = await _context.PosUsers
                .Include(u => u.UserGroup)
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            if (user == null)
                return null;

            var locationClaim = principal.FindFirst("location_id");
            var locationId = locationClaim != null && Guid.TryParse(locationClaim.Value, out var locId)
                ? locId
                : user.HomeLocationId;

            return await GenerateLoginResponse(user, locationId);
        }
        catch
        {
            return null;
        }
    }

    public string HashPin(string pin)
    {
        using var sha256 = SHA256.Create();
        var salt = Guid.NewGuid().ToString();
        var hash = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(pin + salt)));
        return $"{salt}:{hash}";
    }

    public bool VerifyPin(string pin, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 2)
            return false;

        var salt = parts[0];
        var hash = parts[1];

        using var sha256 = SHA256.Create();
        var computedHash = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(pin + salt)));
        return hash == computedHash;
    }

    private async Task<LoginResponse> GenerateLoginResponse(PosUser user, Guid locationId)
    {
        var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? "default-dev-key-minimum-32-chars!");
        var issuer = _configuration["Jwt:Issuer"] ?? "DarkVelocity";
        var audience = _configuration["Jwt:Audience"] ?? "DarkVelocity";
        var expiresInMinutes = int.Parse(_configuration["Jwt:ExpiresInMinutes"] ?? "60");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.GivenName, user.FirstName),
            new(ClaimTypes.Surname, user.LastName),
            new("user_group", user.UserGroup?.Name ?? ""),
            new("location_id", locationId.ToString()),
            new("home_location_id", user.HomeLocationId.ToString())
        };

        var accessExpires = DateTime.UtcNow.AddMinutes(expiresInMinutes);
        var refreshExpires = DateTime.UtcNow.AddDays(7);

        var accessToken = GenerateJwtToken(claims, key, issuer, audience, accessExpires);
        var refreshToken = GenerateJwtToken(claims, key, issuer, audience, refreshExpires);

        return new LoginResponse(
            accessToken,
            refreshToken,
            accessExpires,
            new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                UserGroupName = user.UserGroup?.Name ?? "",
                HomeLocationId = user.HomeLocationId,
                IsActive = user.IsActive
            }
        );
    }

    private static string GenerateJwtToken(
        IEnumerable<Claim> claims,
        byte[] key,
        string issuer,
        string audience,
        DateTime expires)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(key),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
