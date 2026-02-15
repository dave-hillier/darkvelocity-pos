using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

[GenerateSerializer]
public record RequestRoomReservationCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid RoomTypeId,
    [property: Id(3)] DateOnly CheckInDate,
    [property: Id(4)] DateOnly CheckOutDate,
    [property: Id(5)] int Adults,
    [property: Id(6)] GuestInfo Guest,
    [property: Id(7)] int Children = 0,
    [property: Id(8)] RatePlanType RatePlan = RatePlanType.BestAvailable,
    [property: Id(9)] string? SpecialRequests = null,
    [property: Id(10)] ReservationSource Source = ReservationSource.Direct,
    [property: Id(11)] string? ExternalRef = null,
    [property: Id(12)] Guid? CustomerId = null);

[GenerateSerializer]
public record ModifyRoomReservationCommand(
    [property: Id(0)] DateOnly? NewCheckInDate = null,
    [property: Id(1)] DateOnly? NewCheckOutDate = null,
    [property: Id(2)] Guid? NewRoomTypeId = null,
    [property: Id(3)] int? NewAdults = null,
    [property: Id(4)] int? NewChildren = null,
    [property: Id(5)] string? SpecialRequests = null);

[GenerateSerializer]
public record CancelRoomReservationCommand(
    [property: Id(0)] string Reason,
    [property: Id(1)] Guid CancelledBy);

[GenerateSerializer]
public record CheckInCommand(
    [property: Id(0)] Guid? RoomId = null,
    [property: Id(1)] string? RoomNumber = null);

[GenerateSerializer]
public record AssignRoomCommand(
    [property: Id(0)] Guid RoomId,
    [property: Id(1)] string RoomNumber);

[GenerateSerializer]
public record RoomReservationRequestedResult(
    [property: Id(0)] Guid Id,
    [property: Id(1)] string ConfirmationCode,
    [property: Id(2)] DateTime CreatedAt);

[GenerateSerializer]
public record RequireRoomDepositCommand(
    [property: Id(0)] decimal Amount,
    [property: Id(1)] DateTime RequiredBy);

[GenerateSerializer]
public record RecordRoomDepositPaymentCommand(
    [property: Id(0)] PaymentMethod Method,
    [property: Id(1)] string PaymentReference);

public interface IRoomReservationGrain : IGrainWithStringKey
{
    Task<RoomReservationRequestedResult> RequestAsync(RequestRoomReservationCommand command);
    Task<RoomReservationState> GetStateAsync();
    Task<bool> ExistsAsync();

    // Reservation lifecycle
    Task ConfirmAsync();
    Task ModifyAsync(ModifyRoomReservationCommand command);
    Task CancelAsync(CancelRoomReservationCommand command);

    // Check-in / check-out
    Task CheckInAsync(CheckInCommand command);
    Task AssignRoomAsync(AssignRoomCommand command);
    Task CheckOutAsync();
    Task MarkNoShowAsync(Guid? markedBy = null);

    // Deposit
    Task RequireDepositAsync(RequireRoomDepositCommand command);
    Task RecordDepositPaymentAsync(RecordRoomDepositPaymentCommand command);
}
