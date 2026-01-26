using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Location.Api.Entities;

public class OperatingHours : BaseEntity
{
    public Guid LocationId { get; set; }

    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly OpenTime { get; set; }
    public TimeOnly CloseTime { get; set; }
    public bool IsClosed { get; set; } = false;

    // Navigation
    public Location? Location { get; set; }
}
