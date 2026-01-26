using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Reporting.Api.Entities;

public class StockConsumption : BaseEntity, ILocationScoped
{
    public Guid LocationId { get; set; }
    public Guid OrderId { get; set; }
    public Guid OrderLineId { get; set; }
    public Guid MenuItemId { get; set; }
    public Guid IngredientId { get; set; }
    public string? IngredientName { get; set; }
    public Guid StockBatchId { get; set; }

    public decimal QuantityConsumed { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }

    public DateTime ConsumedAt { get; set; }
}
