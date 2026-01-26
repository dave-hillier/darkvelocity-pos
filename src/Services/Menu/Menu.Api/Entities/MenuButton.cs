using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Menu.Api.Entities;

public class MenuButton : BaseEntity
{
    public Guid ScreenId { get; set; }
    public Guid? ItemId { get; set; }

    public int Row { get; set; }
    public int Column { get; set; }
    public int RowSpan { get; set; } = 1;
    public int ColumnSpan { get; set; } = 1;
    public string? Label { get; set; }
    public string? Color { get; set; }
    public string? ButtonType { get; set; } = "item";

    public MenuScreen? Screen { get; set; }
    public MenuItem? Item { get; set; }
}
