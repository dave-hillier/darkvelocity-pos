using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Payments.Api.Entities;

public class PaymentMethod : BaseEntity, ILocationScoped
{
    public Guid LocationId { get; set; }
    public required string Name { get; set; }
    public required string MethodType { get; set; } // cash, card, voucher
    public bool IsActive { get; set; } = true;
    public bool RequiresTip { get; set; }
    public bool OpensDrawer { get; set; } = true;
    public int DisplayOrder { get; set; }

    // For card payments
    public bool RequiresExternalTerminal { get; set; }

    // Navigation
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
