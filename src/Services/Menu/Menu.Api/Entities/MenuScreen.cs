using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Menu.Api.Entities;

public class MenuScreen : BaseEntity
{
    public Guid MenuId { get; set; }
    public required string Name { get; set; }
    public int Position { get; set; }
    public string? Color { get; set; }
    public int Rows { get; set; } = 4;
    public int Columns { get; set; } = 5;

    public MenuDefinition? Menu { get; set; }
    public ICollection<MenuButton> Buttons { get; set; } = new List<MenuButton>();
}
