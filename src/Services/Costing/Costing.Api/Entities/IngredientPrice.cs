using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Costing.Api.Entities;

public class IngredientPrice : BaseEntity
{
    public Guid IngredientId { get; set; }
    public required string IngredientName { get; set; }

    // Current pricing
    public decimal CurrentPrice { get; set; }
    public required string UnitOfMeasure { get; set; }
    public decimal PackSize { get; set; } = 1; // e.g., 5kg bag
    public decimal PricePerUnit { get; set; } // CurrentPrice / PackSize

    // Supplier info (for reference)
    public Guid? PreferredSupplierId { get; set; }
    public string? PreferredSupplierName { get; set; }

    // Price history tracking
    public decimal? PreviousPrice { get; set; }
    public DateTime? PriceChangedAt { get; set; }
    public decimal PriceChangePercent { get; set; }

    // Status
    public bool IsActive { get; set; } = true;
}
