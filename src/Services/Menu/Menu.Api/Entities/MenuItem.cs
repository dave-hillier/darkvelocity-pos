using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Menu.Api.Entities;

public class MenuItem : BaseEntity, ILocationScoped
{
    public Guid LocationId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid AccountingGroupId { get; set; }
    public Guid? RecipeId { get; set; }

    public required string Name { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public string? Sku { get; set; }
    public bool IsActive { get; set; } = true;
    public bool TrackInventory { get; set; } = true;

    public MenuCategory? Category { get; set; }
    public AccountingGroup? AccountingGroup { get; set; }
    public ICollection<MenuButton> Buttons { get; set; } = new List<MenuButton>();
}
