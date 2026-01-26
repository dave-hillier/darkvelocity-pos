namespace DarkVelocity.Auth.Api.Entities;

public class UserLocationAccess
{
    public Guid UserId { get; set; }
    public PosUser? User { get; set; }

    public Guid LocationId { get; set; }
    public Location? Location { get; set; }
}
