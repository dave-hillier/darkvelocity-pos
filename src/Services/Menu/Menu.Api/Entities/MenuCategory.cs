using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Menu.Api.Entities;

public class MenuCategory : BaseEntity, ILocationScoped
{
    public Guid LocationId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public string? Color { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<MenuItem> Items { get; set; } = new List<MenuItem>();
}
