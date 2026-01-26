using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Auth.Api.Entities;

public class PosUser : BaseAuditableEntity
{
    public required string Username { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? Email { get; set; }
    public required string PinHash { get; set; }
    public string? QrCodeToken { get; set; }
    public bool IsActive { get; set; } = true;

    public Guid UserGroupId { get; set; }
    public UserGroup? UserGroup { get; set; }

    public Guid HomeLocationId { get; set; }
    public Location? HomeLocation { get; set; }

    public ICollection<UserLocationAccess> LocationAccess { get; set; } = new List<UserLocationAccess>();

    public string FullName => $"{FirstName} {LastName}";
}
