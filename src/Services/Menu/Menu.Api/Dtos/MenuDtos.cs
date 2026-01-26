using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Menu.Api.Dtos;

public class AccountingGroupDto : HalResource
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public decimal TaxRate { get; set; }
    public bool IsActive { get; set; }
}

public class CategoryDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public string? Color { get; set; }
    public bool IsActive { get; set; }
    public int ItemCount { get; set; }
}

public class MenuItemDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid AccountingGroupId { get; set; }
    public Guid? RecipeId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public string? Sku { get; set; }
    public bool IsActive { get; set; }
    public bool TrackInventory { get; set; }
    public string? CategoryName { get; set; }
    public string? AccountingGroupName { get; set; }
    public decimal? TaxRate { get; set; }
}

public class MenuDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public List<MenuScreenDto> Screens { get; set; } = new();
}

public class MenuScreenDto : HalResource
{
    public Guid Id { get; set; }
    public Guid MenuId { get; set; }
    public required string Name { get; set; }
    public int Position { get; set; }
    public string? Color { get; set; }
    public int Rows { get; set; }
    public int Columns { get; set; }
    public List<MenuButtonDto> Buttons { get; set; } = new();
}

public class MenuButtonDto : HalResource
{
    public Guid Id { get; set; }
    public Guid ScreenId { get; set; }
    public Guid? ItemId { get; set; }
    public int Row { get; set; }
    public int Column { get; set; }
    public int RowSpan { get; set; }
    public int ColumnSpan { get; set; }
    public string? Label { get; set; }
    public string? Color { get; set; }
    public string? ButtonType { get; set; }
    public MenuItemDto? Item { get; set; }
}

public record CreateAccountingGroupRequest(
    string Name,
    string? Description,
    decimal TaxRate);

public record UpdateAccountingGroupRequest(
    string? Name,
    string? Description,
    decimal? TaxRate,
    bool? IsActive);

public record CreateCategoryRequest(
    string Name,
    string? Description,
    int DisplayOrder,
    string? Color);

public record UpdateCategoryRequest(
    string? Name,
    string? Description,
    int? DisplayOrder,
    string? Color,
    bool? IsActive);

public record CreateMenuItemRequest(
    string Name,
    Guid CategoryId,
    Guid AccountingGroupId,
    decimal Price,
    string? Description,
    string? ImageUrl,
    string? Sku,
    Guid? RecipeId,
    bool TrackInventory = true);

public record UpdateMenuItemRequest(
    string? Name,
    Guid? CategoryId,
    Guid? AccountingGroupId,
    decimal? Price,
    string? Description,
    string? ImageUrl,
    string? Sku,
    Guid? RecipeId,
    bool? TrackInventory,
    bool? IsActive);

public record CreateMenuRequest(
    string Name,
    string? Description,
    bool IsDefault = false);

public record UpdateMenuRequest(
    string? Name,
    string? Description,
    bool? IsDefault,
    bool? IsActive);

public record CreateScreenRequest(
    string Name,
    int Position,
    string? Color,
    int Rows = 4,
    int Columns = 5);

public record CreateButtonRequest(
    int Row,
    int Column,
    Guid? ItemId = null,
    string? Label = null,
    string? Color = null,
    int RowSpan = 1,
    int ColumnSpan = 1,
    string ButtonType = "item");
