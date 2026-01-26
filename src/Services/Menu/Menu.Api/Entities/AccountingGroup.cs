using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Menu.Api.Entities;

public class AccountingGroup : BaseEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public decimal TaxRate { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<MenuItem> Items { get; set; } = new List<MenuItem>();
}
