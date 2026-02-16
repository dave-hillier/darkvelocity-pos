namespace DarkVelocity.Host.State;

// ============================================================================
// Room Domain Enums
// ============================================================================

public enum RoomStatus
{
    Available,
    Occupied,
    Dirty,
    Inspected,
    OutOfOrder,
    OutOfService
}

public enum HousekeepingStatus
{
    Clean,
    Dirty,
    InProgress,
    Inspected
}

public enum ReservationStatus
{
    Requested,
    PendingDeposit,
    Confirmed,
    CheckedIn,
    InHouse,
    CheckedOut,
    NoShow,
    Cancelled
}

public enum ReservationSource
{
    Direct,
    Website,
    Phone,
    WalkIn,
    BookingDotCom,
    Expedia,
    Airbnb,
    OtherOTA,
    TravelAgent,
    Corporate,
    GroupBlock,
    Other
}

public enum RatePlanType
{
    Rack,
    BestAvailable,
    AdvancePurchase,
    NonRefundable,
    MemberRate,
    CorporateRate,
    GroupRate,
    PackageRate,
    Promotional
}

// ============================================================================
// Room Type State
// ============================================================================

[GenerateSerializer]
public sealed class RoomTypeState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public Guid SiteId { get; set; }

    [Id(3)] public string Name { get; set; } = string.Empty;
    [Id(4)] public string? Description { get; set; }
    [Id(5)] public string Code { get; set; } = string.Empty;

    [Id(6)] public int BaseOccupancy { get; set; } = 2;
    [Id(7)] public int MaxOccupancy { get; set; } = 2;
    [Id(8)] public int MaxAdults { get; set; } = 2;
    [Id(9)] public int MaxChildren { get; set; } = 0;

    [Id(10)] public int TotalRooms { get; set; }
    [Id(11)] public List<string> Amenities { get; set; } = [];
    [Id(12)] public List<string> BedConfigurations { get; set; } = [];

    [Id(13)] public decimal RackRate { get; set; }
    [Id(14)] public decimal? ExtraAdultRate { get; set; }
    [Id(15)] public decimal? ExtraChildRate { get; set; }

    [Id(16)] public int SortOrder { get; set; }
    [Id(17)] public bool IsActive { get; set; } = true;

    [Id(18)] public DateTime CreatedAt { get; set; }
    [Id(19)] public DateTime? UpdatedAt { get; set; }
    [Id(20)] public int Version { get; set; }
}

// ============================================================================
// Room State (physical room)
// ============================================================================

[GenerateSerializer]
public sealed class RoomState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public Guid SiteId { get; set; }
    [Id(3)] public Guid RoomTypeId { get; set; }

    [Id(4)] public string Number { get; set; } = string.Empty;
    [Id(5)] public string? Name { get; set; }
    [Id(6)] public int Floor { get; set; }

    [Id(7)] public RoomStatus Status { get; set; } = RoomStatus.Available;
    [Id(8)] public HousekeepingStatus HousekeepingStatus { get; set; } = HousekeepingStatus.Clean;

    [Id(9)] public RoomOccupancy? CurrentOccupancy { get; set; }
    [Id(10)] public List<string> Features { get; set; } = [];
    [Id(11)] public bool IsConnecting { get; set; }
    [Id(12)] public Guid? ConnectingRoomId { get; set; }

    [Id(13)] public DateTime CreatedAt { get; set; }
    [Id(14)] public DateTime? UpdatedAt { get; set; }
    [Id(15)] public int Version { get; set; }
}

[GenerateSerializer]
public record RoomOccupancy
{
    [Id(0)] public Guid ReservationId { get; init; }
    [Id(1)] public string? GuestName { get; init; }
    [Id(2)] public int GuestCount { get; init; }
    [Id(3)] public DateTime CheckedInAt { get; init; }
    [Id(4)] public DateOnly ExpectedCheckOut { get; init; }
}

// ============================================================================
// Room Inventory State (per room type per date)
// ============================================================================

[GenerateSerializer]
public sealed class RoomInventoryState
{
    [Id(0)] public Guid OrganizationId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public Guid RoomTypeId { get; set; }
    [Id(3)] public DateOnly Date { get; set; }

    [Id(4)] public int TotalRooms { get; set; }
    [Id(5)] public int SoldCount { get; set; }
    [Id(6)] public int BlockedCount { get; set; }
    [Id(7)] public int OutOfOrderCount { get; set; }
    [Id(8)] public int OverbookingAllowance { get; set; }

    [Id(9)] public List<RoomInventoryHold> Holds { get; set; } = [];

    [Id(10)] public int Version { get; set; }
}

[GenerateSerializer]
public record RoomInventoryHold
{
    [Id(0)] public Guid HoldId { get; init; }
    [Id(1)] public string Reason { get; init; } = string.Empty;
    [Id(2)] public int RoomCount { get; init; }
    [Id(3)] public DateOnly? ReleaseDate { get; init; }
}

// ============================================================================
// Room Reservation State
// ============================================================================

[GenerateSerializer]
public sealed class RoomReservationState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public Guid SiteId { get; set; }
    [Id(3)] public string ConfirmationCode { get; set; } = string.Empty;
    [Id(4)] public ReservationStatus Status { get; set; } = ReservationStatus.Requested;

    // Stay details
    [Id(5)] public Guid RoomTypeId { get; set; }
    [Id(6)] public DateOnly CheckInDate { get; set; }
    [Id(7)] public DateOnly CheckOutDate { get; set; }
    [Id(8)] public int Adults { get; set; } = 1;
    [Id(9)] public int Children { get; set; }

    // Guest
    [Id(10)] public GuestInfo Guest { get; set; } = new();
    [Id(11)] public Guid? CustomerId { get; set; }

    // Room assignment
    [Id(12)] public Guid? AssignedRoomId { get; set; }
    [Id(13)] public string? AssignedRoomNumber { get; set; }

    // Rate
    [Id(14)] public RatePlanType RatePlan { get; set; } = RatePlanType.BestAvailable;
    [Id(15)] public List<NightlyRate> NightlyRates { get; set; } = [];
    [Id(16)] public decimal TotalRate { get; set; }

    // Policies
    [Id(17)] public string? SpecialRequests { get; set; }
    [Id(18)] public ReservationSource Source { get; set; }
    [Id(19)] public string? ExternalRef { get; set; }
    [Id(20)] public string? Notes { get; set; }

    // Deposit
    [Id(21)] public DepositInfo? Deposit { get; set; }

    // Timestamps
    [Id(22)] public DateTime CreatedAt { get; set; }
    [Id(23)] public DateTime? ConfirmedAt { get; set; }
    [Id(24)] public DateTime? CheckedInAt { get; set; }
    [Id(25)] public DateTime? CheckedOutAt { get; set; }
    [Id(26)] public DateTime? CancelledAt { get; set; }
    [Id(27)] public string? CancellationReason { get; set; }
    [Id(28)] public Guid? CancelledBy { get; set; }

    [Id(29)] public int Version { get; set; }
}

[GenerateSerializer]
public record NightlyRate
{
    [Id(0)] public DateOnly Date { get; init; }
    [Id(1)] public decimal Rate { get; init; }
}

// ============================================================================
// Room Reservation Settings State (per site)
// ============================================================================

[GenerateSerializer]
public sealed class RoomReservationSettingsState
{
    [Id(0)] public Guid OrganizationId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }

    // Check-in / check-out
    [Id(2)] public TimeOnly DefaultCheckInTime { get; set; } = new(15, 0);
    [Id(3)] public TimeOnly DefaultCheckOutTime { get; set; } = new(11, 0);

    // Booking window
    [Id(4)] public int AdvanceBookingDays { get; set; } = 365;
    [Id(5)] public int MinStayNights { get; set; } = 1;
    [Id(6)] public int MaxStayNights { get; set; } = 30;

    // Overbooking
    [Id(7)] public int OverbookingPercent { get; set; }

    // Deposit policies
    [Id(8)] public bool RequireDeposit { get; set; }
    [Id(9)] public decimal DepositAmount { get; set; }
    [Id(10)] public bool DepositIsFirstNight { get; set; } = true;

    // Cancellation
    [Id(11)] public TimeSpan FreeCancellationWindow { get; set; } = TimeSpan.FromHours(48);

    // Closed dates (per room type or site-wide)
    [Id(12)] public List<DateOnly> ClosedToArrivalDates { get; set; } = [];
    [Id(13)] public List<DateOnly> ClosedToDepartureDates { get; set; } = [];

    // Children policy
    [Id(14)] public bool AllowChildren { get; set; } = true;
    [Id(15)] public int ChildMaxAge { get; set; } = 12;

    [Id(16)] public int Version { get; set; }
}
