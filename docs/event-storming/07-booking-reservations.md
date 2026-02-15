# Event Storming: Booking & Reservations Domain

## Overview

The Booking & Reservations domain manages table reservations, waitlists, guest arrivals, and the connection between bookings and orders. This domain enables restaurants to optimize table utilization, manage guest expectations, and provide seamless service from reservation through departure.

---

## Domain Purpose

- **Reservation Management**: Create, modify, and cancel table reservations
- **Availability Calculation**: Determine available time slots based on capacity
- **Guest Communication**: Send confirmations, reminders, and updates
- **Waitlist Management**: Handle walk-ins when capacity is full
- **Deposit Handling**: Collect and manage reservation deposits
- **Table Assignment**: Optimize seating and table utilization
- **Order Integration**: Connect bookings to orders for unified service

---

## Actors

| Actor | Description | Typical Actions |
|-------|-------------|-----------------|
| **Guest** | Customer making reservation | Request booking, modify, cancel |
| **Host/Hostess** | Front-of-house staff | Manage reservations, seat guests |
| **Site Manager** | Venue management | Configure settings, override rules |
| **Server** | Wait staff | Link orders to bookings |
| **System** | Automated processes | Send reminders, mark no-shows |
| **Third-Party Platform** | OpenTable, Resy, etc. | Sync reservations |

---

## Aggregates

### Booking Aggregate

Represents a table reservation.

```
Booking
├── Id: Guid
├── OrgId: Guid
├── SiteId: Guid
├── ConfirmationCode: string
├── Status: BookingStatus
├── RequestedTime: DateTime
├── ConfirmedTime?: DateTime
├── Duration: TimeSpan
├── PartySize: int
├── Guest: GuestInfo
├── CustomerId?: Guid
├── TableAssignments: List<TableAssignment>
├── SpecialRequests?: string
├── Tags: List<string>
├── Occasion?: string
├── Deposit: DepositInfo?
├── Source: BookingSource
├── ExternalRef?: string
├── Notes?: string
├── CreatedAt: DateTime
├── ConfirmedAt?: DateTime
├── ArrivedAt?: DateTime
├── SeatedAt?: DateTime
├── DepartedAt?: DateTime
├── CancelledAt?: DateTime
├── LinkedOrderId?: Guid
└── Metadata: Dictionary<string, string>
```

**Invariants:**
- Party size must be > 0
- Requested time must be during operating hours
- Confirmed time must have available capacity
- Cannot modify after seated (without manager override)
- Deposit required based on party size/time configuration

### GuestInfo Value Object

```
GuestInfo
├── Name: string
├── Phone?: string
├── Email?: string
├── Notes?: string (allergies, preferences)
├── VipStatus: VipStatus
└── VisitCount: int
```

### DepositInfo Value Object

```
DepositInfo
├── Amount: decimal
├── Status: DepositStatus
├── RequiredAt: DateTime
├── PaidAt?: DateTime
├── PaymentMethod?: PaymentMethod
├── PaymentReference?: string
├── ForfeitedAt?: DateTime
├── RefundedAt?: DateTime
└── RefundReason?: string
```

### BookingCalendar Aggregate

Manages availability and capacity for a site.

```
BookingCalendar
├── SiteId: Guid
├── Date: DateOnly
├── Slots: List<TimeSlot>
├── SpecialHours?: OperatingHours
├── BlockedPeriods: List<BlockedPeriod>
├── CapacityOverrides: List<CapacityOverride>
├── MaxCoversPerSlot: int
├── MinutesPerSlot: int
├── TurnTime: int (minutes)
└── Bookings: List<BookingReference>
```

### TimeSlot Value Object

```
TimeSlot
├── StartTime: TimeOnly
├── EndTime: TimeOnly
├── TotalCapacity: int
├── BookedCovers: int
├── AvailableCovers: int
├── Status: SlotStatus
└── BlockReason?: string
```

### Waitlist Aggregate

Manages walk-in queue for a site.

```
Waitlist
├── SiteId: Guid
├── Date: DateOnly
├── Entries: List<WaitlistEntry>
├── CurrentPosition: int
└── AverageWait: TimeSpan
```

### WaitlistEntry Entity

```
WaitlistEntry
├── Id: Guid
├── Position: int
├── Guest: GuestInfo
├── PartySize: int
├── CheckedInAt: DateTime
├── QuotedWait: TimeSpan
├── Status: WaitlistStatus
├── TablePreferences?: string
├── NotificationMethod: NotificationMethod
├── NotifiedAt?: DateTime
├── SeatedAt?: DateTime
├── LeftAt?: DateTime
└── ConvertedToBookingId?: Guid
```

---

## Booking State Machine

```
┌─────────────┐
│  Requested  │
└──────┬──────┘
       │
       │ (if auto-confirm or host confirm)
       ├─────────────────────────────────┐
       │                                 │
       ▼                                 ▼
┌─────────────┐                  ┌─────────────┐
│  Confirmed  │                  │  Pending    │
│             │                  │  Deposit    │
└──────┬──────┘                  └──────┬──────┘
       │                                │
       │                                │ Deposit Paid
       │                                ▼
       │                         ┌─────────────┐
       │                         │  Confirmed  │
       │                         └──────┬──────┘
       │                                │
       ├────────────────────────────────┘
       │
       │ Guest Arrives (before/on time)
       ▼
┌─────────────┐
│   Arrived   │
└──────┬──────┘
       │
       │ Seated at table
       ▼
┌─────────────┐
│   Seated    │
└──────┬──────┘
       │
       │ Guest leaves
       ▼
┌─────────────┐
│  Completed  │
└─────────────┘

From Confirmed:
       │
       │ No-show (past grace period)
       ▼
┌─────────────┐
│   No-Show   │
└─────────────┘

From Requested/Confirmed:
       │
       │ Cancel
       ▼
┌─────────────┐
│  Cancelled  │
└─────────────┘
```

---

## Commands

### Booking Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `RequestBooking` | Create new reservation | Within booking window, capacity available | Guest, Host |
| `ConfirmBooking` | Confirm reservation | Booking requested | Host, System |
| `ModifyBooking` | Change date/time/size | Not seated, availability exists | Guest, Host |
| `CancelBooking` | Cancel reservation | Not seated | Guest, Host |
| `AddSpecialRequest` | Note guest requests | Booking exists | Guest, Host |
| `AssignTable` | Pre-assign table | Booking confirmed | Host |
| `RecordArrival` | Mark guest arrived | Booking confirmed | Host |
| `SeatGuest` | Assign to table | Guest arrived | Host |
| `RecordDeparture` | Mark guest left | Guest seated | Host |
| `MarkNoShow` | Flag as no-show | Past grace period | System, Host |
| `LinkToOrder` | Connect to order | Guest seated, order exists | Server |
| `AddGuestNote` | Add notes for service | Booking exists | Host |

### Deposit Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `RequireDeposit` | Set deposit requirement | Booking confirmed | System, Host |
| `RecordDepositPayment` | Mark deposit paid | Deposit required | Guest, Host |
| `ForfeitDeposit` | Keep deposit on no-show | No-show, policy allows | System |
| `RefundDeposit` | Return deposit | Deposit paid | Host, Manager |
| `WaiveDeposit` | Remove requirement | Deposit required | Manager |

### Calendar Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `ConfigureSlots` | Set time slots | Calendar exists | Manager |
| `BlockTimeSlot` | Close a slot | Slot exists | Manager |
| `UnblockTimeSlot` | Reopen a slot | Slot blocked | Manager |
| `OverrideCapacity` | Adjust capacity | Calendar exists | Manager |
| `SetSpecialHours` | Holiday hours | Calendar exists | Manager |

### Waitlist Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `AddToWaitlist` | Add walk-in | Waitlist active | Host |
| `UpdateWaitPosition` | Adjust position | Entry exists | Host |
| `NotifyForTable` | Alert guest | Table available | System, Host |
| `SeatFromWaitlist` | Move to table | Entry notified | Host |
| `RemoveFromWaitlist` | Guest leaves | Entry exists | Host |
| `ConvertToBooking` | Future booking | Entry exists | Host |

---

## Domain Events

### Booking Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `BookingRequested` | Reservation created | BookingId, Guest, Time, PartySize | RequestBooking |
| `BookingConfirmed` | Reservation confirmed | BookingId, ConfirmedTime | ConfirmBooking |
| `BookingModified` | Details changed | BookingId, OldValues, NewValues | ModifyBooking |
| `BookingCancelled` | Reservation cancelled | BookingId, Reason, CancelledBy | CancelBooking |
| `SpecialRequestAdded` | Guest request noted | BookingId, Request | AddSpecialRequest |
| `TableAssigned` | Table pre-assigned | BookingId, TableId | AssignTable |
| `GuestArrived` | Guest checked in | BookingId, ArrivalTime, EarlyLate | RecordArrival |
| `GuestSeated` | Seated at table | BookingId, TableId, SeatedAt | SeatGuest |
| `GuestDeparted` | Guest left | BookingId, Duration, OrderTotal | RecordDeparture |
| `BookingNoShow` | No-show flagged | BookingId, DepositForfeited | MarkNoShow |
| `BookingLinkedToOrder` | Connected to order | BookingId, OrderId | LinkToOrder |
| `GuestNoteAdded` | Service note added | BookingId, Note | AddGuestNote |

### Deposit Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `DepositRequirementSet` | Deposit needed | BookingId, Amount, DueBy | RequireDeposit |
| `DepositPaid` | Deposit received | BookingId, Amount, PaymentRef | RecordDepositPayment |
| `DepositForfeited` | Deposit kept | BookingId, Amount, Reason | ForfeitDeposit |
| `DepositRefunded` | Deposit returned | BookingId, Amount, Reason | RefundDeposit |
| `DepositWaived` | Requirement removed | BookingId, WaivedBy | WaiveDeposit |

### Calendar Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `SlotsConfigured` | Time slots set | SiteId, Date, Slots | ConfigureSlots |
| `TimeSlotBlocked` | Slot closed | SiteId, Date, Slot, Reason | BlockTimeSlot |
| `TimeSlotUnblocked` | Slot reopened | SiteId, Date, Slot | UnblockTimeSlot |
| `CapacityOverridden` | Capacity changed | SiteId, Date, OldCap, NewCap | OverrideCapacity |
| `SpecialHoursSet` | Holiday hours set | SiteId, Date, Hours | SetSpecialHours |

### Waitlist Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `WaitlistEntryAdded` | Walk-in added | EntryId, Guest, PartySize, QuotedWait | AddToWaitlist |
| `WaitlistPositionUpdated` | Position changed | EntryId, OldPosition, NewPosition | UpdateWaitPosition |
| `WaitlistNotificationSent` | Table ready notice | EntryId, NotificationMethod | NotifyForTable |
| `WaitlistEntrySeated` | Seated from wait | EntryId, TableId, WaitDuration | SeatFromWaitlist |
| `WaitlistEntryRemoved` | Guest left queue | EntryId, Reason | RemoveFromWaitlist |
| `WaitlistConvertedToBooking` | Future booking made | EntryId, BookingId | ConvertToBooking |

---

## Event Details

### BookingRequested

```csharp
public record BookingRequested : DomainEvent
{
    public override string EventType => "bookings.booking.requested";

    public required Guid BookingId { get; init; }
    public required Guid SiteId { get; init; }
    public required string ConfirmationCode { get; init; }
    public required GuestInfo Guest { get; init; }
    public Guid? CustomerId { get; init; }
    public required DateTime RequestedTime { get; init; }
    public required int PartySize { get; init; }
    public required TimeSpan Duration { get; init; }
    public string? SpecialRequests { get; init; }
    public string? Occasion { get; init; }
    public required BookingSource Source { get; init; }
    public string? ExternalRef { get; init; }
    public bool DepositRequired { get; init; }
    public decimal? DepositAmount { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public enum BookingSource
{
    Direct,        // In-house
    Website,       // Own website
    Phone,
    WalkIn,
    OpenTable,
    Resy,
    GoogleMaps,
    Facebook,
    Other
}
```

### GuestArrived

```csharp
public record GuestArrived : DomainEvent
{
    public override string EventType => "bookings.booking.guest_arrived";

    public required Guid BookingId { get; init; }
    public required Guid SiteId { get; init; }
    public required string GuestName { get; init; }
    public required int PartySize { get; init; }
    public required DateTime BookedTime { get; init; }
    public required DateTime ArrivalTime { get; init; }
    public required int MinutesEarlyLate { get; init; } // negative = early
    public required Guid CheckedInBy { get; init; }
}
```

### GuestSeated

```csharp
public record GuestSeated : DomainEvent
{
    public override string EventType => "bookings.booking.guest_seated";

    public required Guid BookingId { get; init; }
    public required Guid SiteId { get; init; }
    public required Guid TableId { get; init; }
    public required string TableNumber { get; init; }
    public Guid? SecondaryTableId { get; init; } // For large parties
    public required int PartySize { get; init; }
    public required DateTime SeatedAt { get; init; }
    public required Guid SeatedBy { get; init; }
    public required TimeSpan WaitTime { get; init; } // From arrival to seated
}
```

### BookingNoShow

```csharp
public record BookingNoShow : DomainEvent
{
    public override string EventType => "bookings.booking.no_show";

    public required Guid BookingId { get; init; }
    public required Guid SiteId { get; init; }
    public required string GuestName { get; init; }
    public required string? GuestPhone { get; init; }
    public required string? GuestEmail { get; init; }
    public required DateTime BookedTime { get; init; }
    public required DateTime MarkedAt { get; init; }
    public required int GracePeriodMinutes { get; init; }
    public required bool DepositForfeited { get; init; }
    public decimal? ForfeitedAmount { get; init; }
    public Guid? MarkedBy { get; init; } // null if system
    public bool WasContacted { get; init; }
}
```

---

## Policies (Event Reactions)

### When BookingRequested

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Auto-Confirm Small Parties | Confirm if < threshold | Booking |
| Send Confirmation | Email/SMS confirmation | Notifications |
| Request Deposit | If policy requires | Payments |
| Link Customer | Match to existing customer | Customer |
| Update Availability | Reduce available slots | Calendar |

### When BookingConfirmed

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Send Confirmation | Final confirmation message | Notifications |
| Schedule Reminder | Set up reminder 24h before | Scheduler |
| Reserve Table | If auto-assign enabled | Tables |
| Update Customer History | Record reservation | Customer |

### When GuestArrived

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Update Table Status | Mark assigned table preparing | Tables |
| Notify Server | Alert assigned server | Notifications |
| Check Special Requests | Display requests to host | Display |
| Start Wait Timer | Track seating time | Metrics |

### When GuestSeated

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Occupy Table | Mark table occupied | Tables |
| Create Order Shell | Optional pre-create order | Orders |
| Notify Server | Alert of new table | Notifications |
| Start Service Timer | Track dining duration | Metrics |

### When BookingNoShow

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Forfeit Deposit | Keep deposit if policy | Payments |
| Update Customer Record | Flag no-show history | Customer |
| Release Table | Free table for walk-ins | Tables |
| Send No-Show Email | Notify guest | Notifications |
| Update Analytics | Track no-show rate | Reporting |

### When GuestDeparted

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Clear Table | Mark for turnover | Tables |
| Complete Booking | Close booking record | Booking |
| Request Review | Send review request | Notifications |
| Update Customer Spend | Record visit value | Customer |
| Calculate Turn Time | Update average times | Metrics |

---

## Read Models / Projections

### BookingListView

```csharp
public record BookingListView
{
    public Guid Id { get; init; }
    public string ConfirmationCode { get; init; }
    public BookingStatus Status { get; init; }
    public DateTime Time { get; init; }
    public int PartySize { get; init; }
    public string GuestName { get; init; }
    public string? GuestPhone { get; init; }
    public VipStatus VipStatus { get; init; }
    public string? TableNumber { get; init; }
    public bool HasSpecialRequests { get; init; }
    public bool HasDeposit { get; init; }
    public string? Occasion { get; init; }
    public BookingSource Source { get; init; }
    public string? Notes { get; init; }
}
```

### AvailabilityView

```csharp
public record AvailabilityView
{
    public Guid SiteId { get; init; }
    public DateOnly Date { get; init; }
    public IReadOnlyList<SlotAvailability> Slots { get; init; }
    public int TotalCapacity { get; init; }
    public int BookedCovers { get; init; }
    public int AvailableCovers { get; init; }
}

public record SlotAvailability
{
    public TimeOnly Time { get; init; }
    public int Available { get; init; }
    public int Booked { get; init; }
    public SlotStatus Status { get; init; }
    public IReadOnlyList<int> AvailableForPartySize { get; init; } // [2, 4, 6]
}
```

### FloorPlanBookingView

```csharp
public record FloorPlanBookingView
{
    public Guid SiteId { get; init; }
    public DateTime AsOf { get; init; }
    public IReadOnlyList<TableBookingState> Tables { get; init; }
    public IReadOnlyList<BookingListView> UpcomingArrivals { get; init; }
    public IReadOnlyList<WaitlistEntryView> Waitlist { get; init; }
}

public record TableBookingState
{
    public Guid TableId { get; init; }
    public string TableNumber { get; init; }
    public int Capacity { get; init; }
    public TableStatus Status { get; init; }
    public BookingInfo? CurrentBooking { get; init; }
    public BookingInfo? NextBooking { get; init; }
    public TimeSpan? TimeUntilNext { get; init; }
    public string? ServerName { get; init; }
}

public record BookingInfo
{
    public Guid BookingId { get; init; }
    public string GuestName { get; init; }
    public int PartySize { get; init; }
    public DateTime Time { get; init; }
    public VipStatus VipStatus { get; init; }
    public string? SpecialRequests { get; init; }
}
```

### WaitlistView

```csharp
public record WaitlistView
{
    public Guid SiteId { get; init; }
    public int TotalWaiting { get; init; }
    public TimeSpan AverageWait { get; init; }
    public IReadOnlyList<WaitlistEntryView> Entries { get; init; }
}

public record WaitlistEntryView
{
    public Guid EntryId { get; init; }
    public int Position { get; init; }
    public string GuestName { get; init; }
    public int PartySize { get; init; }
    public DateTime CheckedInAt { get; init; }
    public TimeSpan Waiting { get; init; }
    public TimeSpan QuotedWait { get; init; }
    public WaitlistStatus Status { get; init; }
    public string? Preferences { get; init; }
    public bool Notified { get; init; }
}
```

### BookingAnalytics

```csharp
public record BookingAnalytics
{
    public Guid SiteId { get; init; }
    public DateRange Period { get; init; }

    // Volume Metrics
    public int TotalBookings { get; init; }
    public int ConfirmedBookings { get; init; }
    public int CancelledBookings { get; init; }
    public int NoShows { get; init; }
    public int WalkIns { get; init; }

    // Rates
    public decimal ConfirmationRate { get; init; }
    public decimal CancellationRate { get; init; }
    public decimal NoShowRate { get; init; }
    public decimal OccupancyRate { get; init; }

    // Timing
    public decimal AverageLeadTimeDays { get; init; }
    public TimeSpan AverageDiningDuration { get; init; }
    public TimeSpan AverageTurnTime { get; init; }
    public TimeSpan AverageWaitlistWait { get; init; }

    // Financial
    public decimal TotalDepositCollected { get; init; }
    public decimal TotalDepositForfeited { get; init; }
    public decimal AverageCheckPerBooking { get; init; }

    // By Source
    public IReadOnlyDictionary<BookingSource, int> BySource { get; init; }
    public IReadOnlyDictionary<DayOfWeek, int> ByDayOfWeek { get; init; }
    public IReadOnlyDictionary<TimeOnly, int> ByTimeSlot { get; init; }
}
```

---

## Bounded Context Relationships

### Upstream Contexts (This domain provides data to)

| Context | Relationship | Data Provided |
|---------|--------------|---------------|
| Orders | Published Language | Booking info for order context |
| Tables | Published Language | Table occupation state |
| Reporting | Published Language | Reservation analytics |
| Customer | Published Language | Visit history |

### Downstream Contexts (This domain consumes from)

| Context | Relationship | Data Consumed |
|---------|--------------|---------------|
| Site | Customer/Supplier | Operating hours, capacity |
| Tables | Customer/Supplier | Table availability |
| Customer | Customer/Supplier | Customer preferences, history |
| Payments | Customer/Supplier | Deposit processing |

---

## Process Flows

### Reservation Flow

```
┌─────────────┐   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐
│    Guest    │   │   Booking   │   │  Calendar   │   │  Notifications│
└──────┬──────┘   └──────┬──────┘   └──────┬──────┘   └──────┬──────┘
       │                 │                 │                 │
       │ Request         │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │                 │ Check Avail     │                 │
       │                 │────────────────>│                 │
       │                 │                 │                 │
       │                 │ Available       │                 │
       │                 │<────────────────│                 │
       │                 │                 │                 │
       │                 │ Reserve Slot    │                 │
       │                 │────────────────>│                 │
       │                 │                 │                 │
       │                 │ BookingRequested│                 │
       │                 │────┐            │                 │
       │                 │<───┘            │                 │
       │                 │                 │                 │
       │                 │ Send Confirm    │                 │
       │                 │────────────────────────────────>│
       │                 │                 │                 │
       │  Confirmation   │                 │    Email/SMS    │
       │<────────────────│                 │────────────────>│
       │                 │                 │                 │
```

### Walk-In to Waitlist Flow

```
┌─────────────┐   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐
│    Host     │   │  Waitlist   │   │   Tables    │   │    Guest    │
└──────┬──────┘   └──────┬──────┘   └──────┬──────┘   └──────┬──────┘
       │                 │                 │                 │
       │ Add to Waitlist │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │   Quote Wait    │                 │                 │
       │<────────────────│                 │                 │
       │                 │                 │                 │
       │            ...time passes...      │                 │
       │                 │                 │                 │
       │                 │ Table Available │                 │
       │                 │<────────────────│                 │
       │                 │                 │                 │
       │                 │ Notify Guest    │                 │
       │                 │────────────────────────────────>│
       │                 │                 │                 │
       │                 │                 │    Guest Ready  │
       │                 │                 │<────────────────│
       │                 │                 │                 │
       │ Seat from List  │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │                 │ Occupy Table    │                 │
       │                 │────────────────>│                 │
       │                 │                 │                 │
```

### Arrival & Seating Flow

```
┌─────────────┐   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐
│    Host     │   │   Booking   │   │    Table    │   │   Server    │
└──────┬──────┘   └──────┬──────┘   └──────┬──────┘   └──────┬──────┘
       │                 │                 │                 │
       │ Record Arrival  │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │                 │ GuestArrived    │                 │
       │                 │────────────────>│                 │
       │                 │                 │                 │
       │                 │ Prepare Table   │                 │
       │                 │────────────────>│                 │
       │                 │                 │                 │
       │ Seat Guest      │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │                 │ GuestSeated     │                 │
       │                 │────────────────>│                 │
       │                 │                 │                 │
       │                 │ Notify Server   │                 │
       │                 │────────────────────────────────>│
       │                 │                 │                 │
       │                 │                 │    Greet Table  │
       │                 │                 │<────────────────│
       │                 │                 │                 │
```

---

## Business Rules

### Booking Rules

1. **Advance Booking Window**: Configurable max days ahead (e.g., 30 days). Prevents booking too far in the future
2. **Minimum Lead Time (Close to Arrival)**: Configurable minimum hours before a slot that online bookings close (e.g., 2 hours). A 7pm slot with a 2-hour lead time stops accepting online bookings at 5pm. Staff can still book inside the cutoff
3. **Party Size Limits**: Min and max party size per booking source. Online max may be lower (e.g., 8) than phone/staff max (e.g., 20)
4. **Duration Estimates by Meal Period**: Auto-calculate expected dining duration based on party size and meal period. Lunch defaults shorter (60 min) than dinner (90–120 min). Configurable per meal period
5. **Table Combinations**: Large parties may need multiple tables. Combination rules constrain which tables can join (adjacency, same section, compatible shapes)
6. **Double Booking Prevention**: Time buffer (turnover buffer) between seatings to allow table clearing and resetting
7. **Last Seating Time**: Distinct from venue close time. No new seatings after this time (e.g., 60–90 min before close). Configurable per meal period. Prevents guests arriving too close to close with insufficient time to dine
8. **Blocked Dates**: Specific dates where no bookings are accepted (private events, holidays, refurbishments)

### Pacing & Staggering Rules

These rules prevent the kitchen and front-of-house from being overwhelmed by too many simultaneous arrivals. `MaxBookingsPerSlot` is a blunt instrument — pacing rules provide finer control.

1. **Covers per Interval**: Maximum total covers (not just booking count) arriving within a configurable window (e.g., max 30 covers in any 15-minute window). A booking for 8 consumes more pacing capacity than a booking for 2
2. **Covers per Meal Period**: Maximum total covers for an entire meal period (e.g., max 80 covers for dinner service). Once the dinner cap is reached, remaining dinner slots close
3. **Stagger Across Adjacent Slots**: Limit covers across a rolling window wider than a single slot (e.g., max 40 covers across any 30-minute window). Prevents clustering where back-to-back slots are each at their individual max
4. **Section Pacing**: Per-section cover limits within a pacing window. Prevents all arrivals being seated in one section while others sit empty
5. **Kitchen Pacing Integration**: Feed booking arrival forecasts to the kitchen display system so the kitchen can anticipate upcoming order volume. Not a hard constraint on bookings, but an information flow

### Channel Quota Rules

Control how booking capacity is distributed across sources to protect walk-in and direct capacity.

1. **Walk-in Hold-back**: Reserve a percentage of capacity for walk-ins (e.g., hold 20% of tables unreservable online). Configurable by meal period — lunch may reserve less than dinner
2. **Per-Channel Caps**: Maximum bookings or covers per channel per day or meal period (e.g., max 10 covers from OpenTable for Friday dinner). Prevents any single third-party platform from consuming all capacity
3. **Priority Ordering**: When multiple channels compete for remaining capacity, a priority order determines which channels close first (e.g., third-party channels close before direct website, which closes before phone)
4. **Staff Override**: Staff can always book regardless of channel quota state. Quotas only apply to automated/self-service channels

### Table Assignment Rules

1. **Minimum Party Size Enforcement**: Tables have `MinCapacity` and `MaxCapacity`. A party of 2 should not be assigned a 10-top when a 2-top is available. The optimizer should enforce minimum, not just score preference
2. **Table Combination Constraints**: Which tables can combine — based on adjacency (same section or explicitly marked as combinable pairs), compatible shape, and maximum combination size (e.g., max 3 tables in a combination)
3. **VIP Table Reservation**: Hold specific tables (e.g., window, chef's table) for VIP-tier guests until a configurable cutoff time. After the cutoff, release to general availability
4. **Server Workload Hard Cap**: Maximum covers per server per service period. Once a server section hits the cap, the optimizer must assign to other sections even if the table fit is slightly worse

### Deposit Rules

1. **Threshold Trigger**: Deposits for parties > X or during prime time periods
2. **Amount Calculation**: Per-person or fixed amount, configurable by meal period
3. **Payment Deadline**: Must pay within X hours of booking or auto-cancel
4. **Cancellation Window**: Full refund if cancelled > 24h before. Configurable per deposit policy
5. **No-Show Policy**: Forfeit deposit on no-show
6. **Prime Time Deposits**: Require deposits for all bookings during designated peak periods regardless of party size

### Confirmation Rules

1. **Auto-Confirm**: Small parties (e.g., ≤ 4) auto-confirmed
2. **Manual Review**: Large parties or special times need staff review before confirmation
3. **Confirmation Required**: Guest must confirm within X hours or booking auto-cancels
4. **Reminder Schedule**: 24h and 2h before reminders. Configurable per venue
5. **Reconfirmation for Large Parties**: Parties above a threshold must reconfirm 48h before

### No-Show Rules

1. **Grace Period**: Wait X minutes past booking time (e.g., 15 min) before marking
2. **Contact Attempt**: Try to reach guest (SMS/call) before marking as no-show
3. **Table Release**: Release table after grace period for walk-ins or waitlist
4. **History Tracking**: Track guest no-show history on customer profile
5. **Blocking**: Option to block repeat no-showers from online booking (e.g., 3 no-shows in 6 months)
6. **Deposit Forfeiture**: Auto-forfeit deposit when marked as no-show

### Meal Period Turn Time Rules

Turn time data should feed back into availability calculations rather than using a flat default duration for all bookings.

1. **Meal Period Definitions**: Configurable named periods (Breakfast, Lunch, Afternoon, Dinner, Late Night) with start/end times per day of week
2. **Default Duration by Meal Period**: Each meal period has its own default booking duration (e.g., Lunch: 60 min, Dinner: 90 min, Late Night: 120 min)
3. **Duration by Party Size**: Larger parties take longer. Scale duration by party size bracket (e.g., 1–2: base, 3–4: +15 min, 5–6: +30 min, 7+: +45 min)
4. **Analytics-Driven Duration**: When sufficient historical turn time data exists, use actual median turn times (by party size + day + meal period) instead of defaults. Fall back to defaults when data is insufficient
5. **Duration Override**: Staff can manually set a specific duration on any individual booking, overriding all calculated values

---

## Integration Points

### Third-Party Platforms

```csharp
// Sync reservations from external platforms
public interface IExternalBookingAdapter
{
    Task<IEnumerable<ExternalBooking>> GetNewBookingsAsync(DateTime since);
    Task<bool> ConfirmBookingAsync(string externalRef);
    Task<bool> CancelBookingAsync(string externalRef, string reason);
    Task SyncTableAvailabilityAsync(Guid siteId, DateOnly date);
}

// OpenTable adapter example
public class OpenTableAdapter : IExternalBookingAdapter
{
    public async Task<IEnumerable<ExternalBooking>> GetNewBookingsAsync(DateTime since)
    {
        // Poll OpenTable API
        var response = await _httpClient.GetAsync(
            $"/bookings?since={since:O}&restaurant_id={_restaurantId}");

        return MapToExternalBookings(response);
    }
}
```

### Customer Integration

```csharp
// Link booking to customer profile
public async Task OnBookingConfirmed(BookingConfirmed @event)
{
    // Find or create customer
    var customerId = @event.CustomerId ?? await FindOrCreateCustomer(@event.Guest);

    if (customerId.HasValue)
    {
        var customerGrain = _grainFactory.GetGrain<ICustomerGrain>(
            GrainKeys.OrgEntity(@event.OrgId, "customer", customerId.Value));

        await customerGrain.RecordVisitAsync(new RecordVisitCommand(
            @event.SiteId,
            @event.BookingId,
            @event.RequestedTime,
            VisitType.Reservation));
    }
}
```

---

## Event Type Registry

```csharp
public static class BookingEventTypes
{
    // Booking Lifecycle
    public const string BookingRequested = "bookings.booking.requested";
    public const string BookingConfirmed = "bookings.booking.confirmed";
    public const string BookingModified = "bookings.booking.modified";
    public const string BookingCancelled = "bookings.booking.cancelled";
    public const string SpecialRequestAdded = "bookings.booking.special_request_added";
    public const string TableAssigned = "bookings.booking.table_assigned";
    public const string GuestArrived = "bookings.booking.guest_arrived";
    public const string GuestSeated = "bookings.booking.guest_seated";
    public const string GuestDeparted = "bookings.booking.guest_departed";
    public const string BookingNoShow = "bookings.booking.no_show";
    public const string BookingLinkedToOrder = "bookings.booking.linked_to_order";
    public const string GuestNoteAdded = "bookings.booking.guest_note_added";

    // Deposits
    public const string DepositRequirementSet = "bookings.deposit.requirement_set";
    public const string DepositPaid = "bookings.deposit.paid";
    public const string DepositForfeited = "bookings.deposit.forfeited";
    public const string DepositRefunded = "bookings.deposit.refunded";
    public const string DepositWaived = "bookings.deposit.waived";

    // Calendar
    public const string SlotsConfigured = "bookings.calendar.slots_configured";
    public const string TimeSlotBlocked = "bookings.calendar.slot_blocked";
    public const string TimeSlotUnblocked = "bookings.calendar.slot_unblocked";
    public const string CapacityOverridden = "bookings.calendar.capacity_overridden";
    public const string SpecialHoursSet = "bookings.calendar.special_hours_set";

    // Waitlist
    public const string WaitlistEntryAdded = "bookings.waitlist.entry_added";
    public const string WaitlistPositionUpdated = "bookings.waitlist.position_updated";
    public const string WaitlistNotificationSent = "bookings.waitlist.notification_sent";
    public const string WaitlistEntrySeated = "bookings.waitlist.entry_seated";
    public const string WaitlistEntryRemoved = "bookings.waitlist.entry_removed";
    public const string WaitlistConvertedToBooking = "bookings.waitlist.converted_to_booking";
}
```

---

## Hotspots & Risks

### High Complexity Areas

| Area | Complexity | Mitigation |
|------|------------|------------|
| **Availability Calculation** | Overlapping bookings, turn times | Cache and recalculate on change |
| **Multi-Table Parties** | Combining tables | Pre-define table combinations |
| **Third-Party Sync** | API reliability, conflicts | Robust sync with conflict resolution |
| **Timezone Handling** | Guest vs venue timezone | Always use venue timezone |

### Performance Considerations

| Concern | Impact | Strategy |
|---------|--------|----------|
| **Availability Lookup** | High read frequency | Pre-compute daily availability |
| **Real-time Updates** | Floor plan display | SignalR for live updates |
| **Peak Time Queries** | Many simultaneous requests | Cache availability per slot |
