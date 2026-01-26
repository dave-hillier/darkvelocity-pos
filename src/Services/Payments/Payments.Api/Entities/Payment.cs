using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Payments.Api.Entities;

public class Payment : BaseEntity, ILocationScoped
{
    public Guid LocationId { get; set; }
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
    public Guid PaymentMethodId { get; set; }

    public decimal Amount { get; set; }
    public decimal TipAmount { get; set; }
    public decimal ReceivedAmount { get; set; } // for cash
    public decimal ChangeAmount { get; set; }   // for cash

    public string Status { get; set; } = "pending"; // pending, completed, refunded, voided

    // For card payments (Stripe)
    public string? StripePaymentIntentId { get; set; }
    public string? CardBrand { get; set; }
    public string? CardLastFour { get; set; }

    public DateTime? CompletedAt { get; set; }
    public DateTime? RefundedAt { get; set; }
    public string? RefundReason { get; set; }

    // Navigation
    public PaymentMethod? PaymentMethod { get; set; }
    public Receipt? Receipt { get; set; }
}
