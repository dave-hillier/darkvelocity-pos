using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Auth.Api.Dtos;

public record LoginRequest(string Pin, Guid? LocationId = null);

public record QrLoginRequest(string Token, Guid? LocationId = null);

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User
);

public record RefreshRequest(string RefreshToken);

public class UserDto : HalResource
{
    public Guid Id { get; init; }
    public required string Username { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? Email { get; init; }
    public required string UserGroupName { get; init; }
    public Guid HomeLocationId { get; init; }
    public bool IsActive { get; init; }
}

public class LocationDto : HalResource
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Timezone { get; init; }
    public required string CurrencyCode { get; init; }
    public string? Address { get; init; }
    public bool IsActive { get; init; }
}

public record CreateUserRequest(
    string Username,
    string FirstName,
    string LastName,
    string? Email,
    string Pin,
    Guid UserGroupId,
    Guid HomeLocationId
);

public record UpdateUserRequest(
    string? FirstName,
    string? LastName,
    string? Email,
    string? Pin,
    Guid? UserGroupId,
    bool? IsActive
);
