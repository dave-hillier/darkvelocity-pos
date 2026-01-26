using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Menu.Api.Entities;

public class MenuDefinition : BaseEntity, ILocationScoped
{
    public Guid LocationId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<MenuScreen> Screens { get; set; } = new List<MenuScreen>();
}
