namespace DarkVelocity.Shared.Infrastructure.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public abstract class BaseAuditableEntity : BaseEntity
{
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

public abstract class BaseSoftDeleteEntity : BaseAuditableEntity
{
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }
}
