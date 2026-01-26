namespace DarkVelocity.Shared.Infrastructure.Entities;

public interface ILocationScoped
{
    Guid LocationId { get; set; }
}
