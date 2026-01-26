using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Auth.Api.Entities;

public class UserGroup : BaseEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? PermissionsJson { get; set; }
    public bool IsSystemGroup { get; set; }

    public ICollection<PosUser> Users { get; set; } = new List<PosUser>();
}
