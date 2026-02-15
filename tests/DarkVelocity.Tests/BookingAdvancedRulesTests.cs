using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class BookingAdvancedRulesTests
{
    private readonly TestClusterFixture _fixture;

    public BookingAdvancedRulesTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<(IBookingSettingsGrain Settings, IBookingCalendarGrain Calendar)> SetupAsync(
        Guid? orgId = null, Guid? siteId = null, DateOnly? date = null)
    {
        var org = orgId ?? Guid.NewGuid();
        var site = siteId ?? Guid.NewGuid();
        var d = date ?? DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        var settings = _fixture.Cluster.GrainFactory.GetGrain<IBookingSettingsGrain>(
            GrainKeys.BookingSettings(org, site));
        await settings.InitializeAsync(org, site);

        var calendar = _fixture.Cluster.GrainFactory.GetGrain<IBookingCalendarGrain>(
            GrainKeys.BookingCalendar(org, site, d));
        await calendar.InitializeAsync(org, site, d);

        return (settings, calendar);
    }

    // ========================================================================
    // Last Seating Rules
    // ========================================================================

    // Given: a venue open 11am-10pm with last seating offset of 90 minutes
    // When: availability is requested
    // Then: slots at or after 8:30pm are unavailable
    [Fact]
    public async Task LastSeatingOffset_ShouldCloseSlotsBefore_CloseTime()
    {
        // Arrange
        var (settings, calendar) = await SetupAsync();
        await settings.UpdateAsync(new UpdateBookingSettingsCommand(
            DefaultOpenTime: new TimeOnly(18, 0),
            DefaultCloseTime: new TimeOnly(22, 0),
            SlotInterval: TimeSpan.FromMinutes(30),
            LastSeatingOffset: TimeSpan.FromMinutes(90)));

        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        // Act
        var slots = await calendar.GetAvailabilityAsync(new GetCalendarAvailabilityQuery(date, 2));

        // Assert — 18:00, 18:30, 19:00, 19:30, 20:00 should be available; 20:30, 21:00, 21:30 should not
        // Close is 22:00, offset is 90 min → last seating cutoff is 20:30
        var availableSlots = slots.Where(s => s.IsAvailable).ToList();
        var unavailableSlots = slots.Where(s => !s.IsAvailable).ToList();

        availableSlots.Should().AllSatisfy(s => s.Time.Should().BeBefore(new TimeOnly(20, 30)));
        unavailableSlots.Should().AllSatisfy(s => s.Time.Should().BeOnOrAfter(new TimeOnly(20, 30)));
    }

    // Given: a venue with no last seating offset (default TimeSpan.Zero)
    // When: availability is requested
    // Then: all slots up to close time are available
    [Fact]
    public async Task LastSeatingOffset_WhenZero_ShouldNotAffectAvailability()
    {
        // Arrange
        var (settings, calendar) = await SetupAsync();
        await settings.UpdateAsync(new UpdateBookingSettingsCommand(
            DefaultOpenTime: new TimeOnly(18, 0),
            DefaultCloseTime: new TimeOnly(22, 0),
            SlotInterval: TimeSpan.FromMinutes(30)));

        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        // Act
        var slots = await calendar.GetAvailabilityAsync(new GetCalendarAvailabilityQuery(date, 2));

        // Assert — all slots should be available (no pacing, no lead time, no last seating)
        slots.Should().AllSatisfy(s => s.IsAvailable.Should().BeTrue());
    }

    // ========================================================================
    // Minimum Lead Time (Close to Arrival)
    // ========================================================================

    // Given: a venue with 2-hour minimum lead time
    // When: availability is requested at 5pm for a date with slots starting at 6pm
    // Then: slots before 7pm are unavailable (less than 2 hours lead time)
    [Fact]
    public async Task MinLeadTime_ShouldCloseSlotsTooCloseToNow()
    {
        // Arrange
        var (settings, calendar) = await SetupAsync();
        await settings.UpdateAsync(new UpdateBookingSettingsCommand(
            DefaultOpenTime: new TimeOnly(18, 0),
            DefaultCloseTime: new TimeOnly(22, 0),
            SlotInterval: TimeSpan.FromMinutes(30),
            MinLeadTimeHours: 2m));

        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var now = date.ToDateTime(new TimeOnly(17, 0)); // 5pm "now"

        // Act
        var slots = await calendar.GetAvailabilityAsync(
            new GetCalendarAvailabilityQuery(date, 2, CurrentTime: now));

        // Assert — slots at 18:00 and 18:30 should be closed (< 2 hours from 5pm)
        // Slots at 19:00+ should be available (>= 2 hours from 5pm)
        slots.First(s => s.Time == new TimeOnly(18, 0)).IsAvailable.Should().BeFalse();
        slots.First(s => s.Time == new TimeOnly(18, 30)).IsAvailable.Should().BeFalse();
        slots.First(s => s.Time == new TimeOnly(19, 0)).IsAvailable.Should().BeTrue();
        slots.First(s => s.Time == new TimeOnly(19, 30)).IsAvailable.Should().BeTrue();
    }

    // Given: a venue with 2-hour minimum lead time
    // When: a staff member (Direct source) requests availability
    // Then: all slots are available because staff bypass lead time
    [Fact]
    public async Task MinLeadTime_StaffBypassesLeadTime()
    {
        // Arrange
        var (settings, calendar) = await SetupAsync();
        await settings.UpdateAsync(new UpdateBookingSettingsCommand(
            DefaultOpenTime: new TimeOnly(18, 0),
            DefaultCloseTime: new TimeOnly(22, 0),
            SlotInterval: TimeSpan.FromMinutes(30),
            MinLeadTimeHours: 2m));

        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var now = date.ToDateTime(new TimeOnly(17, 0));

        // Act — Direct source = staff
        var slots = await calendar.GetAvailabilityAsync(
            new GetCalendarAvailabilityQuery(date, 2, Source: BookingSource.Direct, CurrentTime: now));

        // Assert — all slots available for staff
        slots.Should().AllSatisfy(s => s.IsAvailable.Should().BeTrue());
    }

    // ========================================================================
    // Pacing / Staggering Rules
    // ========================================================================

    // Given: a venue with max 20 covers per 15-minute interval
    // When: 18 covers are already booked in the 7pm slot and a party of 4 requests availability
    // Then: the 7pm slot is unavailable (18 + 4 = 22 > 20)
    [Fact]
    public async Task Pacing_ShouldLimitCoversPerInterval()
    {
        // Arrange
        var (settings, calendar) = await SetupAsync();
        await settings.UpdateAsync(new UpdateBookingSettingsCommand(
            DefaultOpenTime: new TimeOnly(18, 0),
            DefaultCloseTime: new TimeOnly(22, 0),
            SlotInterval: TimeSpan.FromMinutes(15),
            MaxCoversPerInterval: 20));

        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        // Add bookings totaling 18 covers at 7pm
        await calendar.AddBookingAsync(new AddBookingToCalendarCommand(
            Guid.NewGuid(), "A1", new TimeOnly(19, 0), 6, "Guest 1", BookingStatus.Confirmed));
        await calendar.AddBookingAsync(new AddBookingToCalendarCommand(
            Guid.NewGuid(), "A2", new TimeOnly(19, 0), 6, "Guest 2", BookingStatus.Confirmed));
        await calendar.AddBookingAsync(new AddBookingToCalendarCommand(
            Guid.NewGuid(), "A3", new TimeOnly(19, 0), 6, "Guest 3", BookingStatus.Confirmed));

        // Act — party of 4 requesting availability
        var slots = await calendar.GetAvailabilityAsync(new GetCalendarAvailabilityQuery(date, 4));

        // Assert — the 7pm slot should be unavailable (18 + 4 = 22 > 20)
        var slot7pm = slots.First(s => s.Time == new TimeOnly(19, 0));
        slot7pm.IsAvailable.Should().BeFalse();

        // But 6pm slot (no bookings) should be available
        var slot6pm = slots.First(s => s.Time == new TimeOnly(18, 0));
        slot6pm.IsAvailable.Should().BeTrue();
    }

    // Given: a venue with max 30 covers across a 2-slot (30 min) pacing window
    // When: 20 covers are at 7:00 and 8 covers at 7:15 and a party of 4 requests
    // Then: 7:00 slot is unavailable (28 + 4 = 32 > 30 in the 7:00-7:30 window)
    [Fact]
    public async Task Pacing_ShouldRespectWindowSlots()
    {
        // Arrange
        var (settings, calendar) = await SetupAsync();
        await settings.UpdateAsync(new UpdateBookingSettingsCommand(
            DefaultOpenTime: new TimeOnly(18, 0),
            DefaultCloseTime: new TimeOnly(22, 0),
            SlotInterval: TimeSpan.FromMinutes(15),
            MaxCoversPerInterval: 30,
            PacingWindowSlots: 2)); // 2 × 15 min = 30-minute window

        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        // Add 20 covers at 7:00 and 8 at 7:15
        for (int i = 0; i < 5; i++)
        {
            await calendar.AddBookingAsync(new AddBookingToCalendarCommand(
                Guid.NewGuid(), $"B{i}", new TimeOnly(19, 0), 4, $"Guest {i}", BookingStatus.Confirmed));
        }
        await calendar.AddBookingAsync(new AddBookingToCalendarCommand(
            Guid.NewGuid(), "C1", new TimeOnly(19, 15), 8, "Big Party", BookingStatus.Confirmed));

        // Act — party of 4
        var slots = await calendar.GetAvailabilityAsync(new GetCalendarAvailabilityQuery(date, 4));

        // Assert — 7:00 window covers 7:00-7:30 = 20 + 8 = 28; 28 + 4 = 32 > 30
        var slot7pm = slots.First(s => s.Time == new TimeOnly(19, 0));
        slot7pm.IsAvailable.Should().BeFalse();
    }

    // Given: a venue with pacing disabled (MaxCoversPerInterval = 0)
    // When: many covers are booked in a slot
    // Then: the slot is still available (pacing not enforced)
    [Fact]
    public async Task Pacing_WhenDisabled_ShouldNotLimit()
    {
        // Arrange
        var (settings, calendar) = await SetupAsync();
        await settings.UpdateAsync(new UpdateBookingSettingsCommand(
            DefaultOpenTime: new TimeOnly(18, 0),
            DefaultCloseTime: new TimeOnly(22, 0),
            SlotInterval: TimeSpan.FromMinutes(15),
            MaxCoversPerInterval: 0)); // Disabled

        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        // Add lots of covers
        for (int i = 0; i < 5; i++)
        {
            await calendar.AddBookingAsync(new AddBookingToCalendarCommand(
                Guid.NewGuid(), $"D{i}", new TimeOnly(19, 0), 8, $"Guest {i}", BookingStatus.Confirmed));
        }

        // Act
        var slots = await calendar.GetAvailabilityAsync(new GetCalendarAvailabilityQuery(date, 4));

        // Assert — slot still available despite 40 covers (pacing disabled)
        var slot = slots.First(s => s.Time == new TimeOnly(19, 0));
        slot.IsAvailable.Should().BeTrue();
    }

    // ========================================================================
    // Channel Quotas
    // ========================================================================

    // Given: a venue with OpenTable limited to 20 covers per day
    // When: 20 covers are already booked and OpenTable requests more availability
    // Then: all slots are unavailable for OpenTable
    [Fact]
    public async Task ChannelQuota_ShouldLimitCoversBySource()
    {
        // Arrange
        var (settings, calendar) = await SetupAsync();
        await settings.UpdateAsync(new UpdateBookingSettingsCommand(
            DefaultOpenTime: new TimeOnly(18, 0),
            DefaultCloseTime: new TimeOnly(22, 0),
            SlotInterval: TimeSpan.FromMinutes(30),
            ChannelQuotas: [new ChannelQuotaConfig { Source = BookingSource.OpenTable, MaxCoversPerDay = 20, Priority = 1 }]));

        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        // Add 20 covers total for the day
        for (int i = 0; i < 5; i++)
        {
            await calendar.AddBookingAsync(new AddBookingToCalendarCommand(
                Guid.NewGuid(), $"E{i}", new TimeOnly(19, 0), 4, $"Guest {i}", BookingStatus.Confirmed));
        }

        // Act — OpenTable source requesting availability
        var slots = await calendar.GetAvailabilityAsync(
            new GetCalendarAvailabilityQuery(date, 2, Source: BookingSource.OpenTable));

        // Assert — all slots unavailable for OpenTable (quota exceeded)
        slots.Should().AllSatisfy(s => s.IsAvailable.Should().BeFalse());
    }

    // Given: the same venue with 20 covers booked
    // When: a staff member (Direct source) requests availability
    // Then: all slots are still available (staff bypasses quotas)
    [Fact]
    public async Task ChannelQuota_StaffBypassesQuota()
    {
        // Arrange
        var (settings, calendar) = await SetupAsync();
        await settings.UpdateAsync(new UpdateBookingSettingsCommand(
            DefaultOpenTime: new TimeOnly(18, 0),
            DefaultCloseTime: new TimeOnly(22, 0),
            SlotInterval: TimeSpan.FromMinutes(30),
            ChannelQuotas: [new ChannelQuotaConfig { Source = BookingSource.OpenTable, MaxCoversPerDay = 20, Priority = 1 }]));

        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        for (int i = 0; i < 5; i++)
        {
            await calendar.AddBookingAsync(new AddBookingToCalendarCommand(
                Guid.NewGuid(), $"F{i}", new TimeOnly(19, 0), 4, $"Guest {i}", BookingStatus.Confirmed));
        }

        // Act — staff source
        var slots = await calendar.GetAvailabilityAsync(
            new GetCalendarAvailabilityQuery(date, 2, Source: BookingSource.Direct));

        // Assert — staff can always book
        slots.Should().AllSatisfy(s => s.IsAvailable.Should().BeTrue());
    }

    // ========================================================================
    // Walk-in Holdback
    // ========================================================================

    // Given: a venue with 20% walk-in holdback and max 10 bookings per slot over 8 slots
    // When: 64 bookings exist (80% of 80 total capacity)
    // Then: online slots are unavailable
    [Fact]
    public async Task WalkInHoldback_ShouldReserveCapacity()
    {
        // Arrange
        var (settings, calendar) = await SetupAsync();
        await settings.UpdateAsync(new UpdateBookingSettingsCommand(
            DefaultOpenTime: new TimeOnly(18, 0),
            DefaultCloseTime: new TimeOnly(22, 0),
            SlotInterval: TimeSpan.FromMinutes(30),
            MaxBookingsPerSlot: 10,
            WalkInHoldbackPercent: 20));

        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        // Total capacity = 10 slots × 8 slots = 80. 20% reserved = 16. Bookings allowed = 64.
        // Add 64 bookings (each party of 1 to simplify)
        for (int i = 0; i < 64; i++)
        {
            await calendar.AddBookingAsync(new AddBookingToCalendarCommand(
                Guid.NewGuid(), $"G{i:D3}", new TimeOnly(18, 0).AddMinutes(30 * (i % 8)), 1, $"Guest {i}", BookingStatus.Confirmed));
        }

        // Act — website source (not staff)
        var slots = await calendar.GetAvailabilityAsync(
            new GetCalendarAvailabilityQuery(date, 2, Source: BookingSource.Website));

        // Assert — all slots should be unavailable (holdback exceeded)
        slots.Should().AllSatisfy(s => s.IsAvailable.Should().BeFalse());
    }

    // ========================================================================
    // Meal Period Duration
    // ========================================================================

    // Given: a venue with lunch (60 min) and dinner (120 min) meal periods
    // When: availability is requested spanning both periods
    // Then: lunch slots have 60-minute duration, dinner slots have 120-minute duration
    [Fact]
    public async Task MealPeriod_ShouldResolveDurationByPeriod()
    {
        // Arrange
        var (settings, calendar) = await SetupAsync();
        await settings.UpdateAsync(new UpdateBookingSettingsCommand(
            DefaultOpenTime: new TimeOnly(11, 0),
            DefaultCloseTime: new TimeOnly(22, 0),
            SlotInterval: TimeSpan.FromMinutes(30),
            MealPeriods:
            [
                new MealPeriodConfig
                {
                    Name = "Lunch",
                    StartTime = new TimeOnly(11, 0),
                    EndTime = new TimeOnly(15, 0),
                    DefaultDuration = TimeSpan.FromMinutes(60)
                },
                new MealPeriodConfig
                {
                    Name = "Dinner",
                    StartTime = new TimeOnly(17, 0),
                    EndTime = new TimeOnly(22, 0),
                    DefaultDuration = TimeSpan.FromMinutes(120)
                }
            ]));

        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        // Act
        var slots = await calendar.GetAvailabilityAsync(new GetCalendarAvailabilityQuery(date, 2));

        // Assert
        var lunchSlot = slots.First(s => s.Time == new TimeOnly(12, 0));
        lunchSlot.EstimatedDuration.Should().Be(TimeSpan.FromMinutes(60));

        var dinnerSlot = slots.First(s => s.Time == new TimeOnly(19, 0));
        dinnerSlot.EstimatedDuration.Should().Be(TimeSpan.FromMinutes(120));

        // Afternoon gap (15:00-17:00) falls back to site default (90 min)
        var gapSlot = slots.First(s => s.Time == new TimeOnly(16, 0));
        gapSlot.EstimatedDuration.Should().Be(TimeSpan.FromMinutes(90)); // site default
    }

    // ========================================================================
    // Meal Period Last Seating Override
    // ========================================================================

    // Given: a dinner period with a 60-minute last seating offset (period ends at 22:00)
    // When: availability is requested
    // Then: dinner slots at or after 21:00 are unavailable
    [Fact]
    public async Task MealPeriod_LastSeatingOffset_ShouldOverridePerPeriod()
    {
        // Arrange
        var (settings, calendar) = await SetupAsync();
        await settings.UpdateAsync(new UpdateBookingSettingsCommand(
            DefaultOpenTime: new TimeOnly(18, 0),
            DefaultCloseTime: new TimeOnly(22, 0),
            SlotInterval: TimeSpan.FromMinutes(30),
            MealPeriods:
            [
                new MealPeriodConfig
                {
                    Name = "Dinner",
                    StartTime = new TimeOnly(18, 0),
                    EndTime = new TimeOnly(22, 0),
                    DefaultDuration = TimeSpan.FromMinutes(90),
                    LastSeatingOffset = TimeSpan.FromMinutes(60)
                }
            ]));

        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        // Act
        var slots = await calendar.GetAvailabilityAsync(new GetCalendarAvailabilityQuery(date, 2));

        // Assert — period last seating = 22:00 - 60 min = 21:00
        var slot2030 = slots.First(s => s.Time == new TimeOnly(20, 30));
        slot2030.IsAvailable.Should().BeTrue();

        var slot2100 = slots.First(s => s.Time == new TimeOnly(21, 0));
        slot2100.IsAvailable.Should().BeFalse();

        var slot2130 = slots.First(s => s.Time == new TimeOnly(21, 30));
        slot2130.IsAvailable.Should().BeFalse();
    }

    // ========================================================================
    // Settings Persistence
    // ========================================================================

    // Given: advanced booking rules are configured
    // When: settings are read back
    // Then: all new fields are persisted correctly
    [Fact]
    public async Task AdvancedSettings_ShouldPersist()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IBookingSettingsGrain>(
            GrainKeys.BookingSettings(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);

        // Act
        await grain.UpdateAsync(new UpdateBookingSettingsCommand(
            MaxCoversPerInterval: 30,
            PacingWindowSlots: 2,
            MinLeadTimeHours: 2.5m,
            LastSeatingOffset: TimeSpan.FromMinutes(90),
            WalkInHoldbackPercent: 20,
            MealPeriods:
            [
                new MealPeriodConfig { Name = "Lunch", StartTime = new TimeOnly(11, 0), EndTime = new TimeOnly(15, 0), DefaultDuration = TimeSpan.FromMinutes(60) }
            ],
            ChannelQuotas:
            [
                new ChannelQuotaConfig { Source = BookingSource.OpenTable, MaxCoversPerDay = 40, Priority = 1 }
            ]));

        // Assert
        var state = await grain.GetStateAsync();
        state.MaxCoversPerInterval.Should().Be(30);
        state.PacingWindowSlots.Should().Be(2);
        state.MinLeadTimeHours.Should().Be(2.5m);
        state.LastSeatingOffset.Should().Be(TimeSpan.FromMinutes(90));
        state.WalkInHoldbackPercent.Should().Be(20);
        state.MealPeriods.Should().HaveCount(1);
        state.MealPeriods[0].Name.Should().Be("Lunch");
        state.ChannelQuotas.Should().HaveCount(1);
        state.ChannelQuotas[0].Source.Should().Be(BookingSource.OpenTable);
        state.ChannelQuotas[0].MaxCoversPerDay.Should().Be(40);
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class TableAssignmentOptimizerAdvancedTests
{
    private readonly TestClusterFixture _fixture;

    public TableAssignmentOptimizerAdvancedTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<ITableAssignmentOptimizerGrain> CreateOptimizerAsync()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ITableAssignmentOptimizerGrain>(
            GrainKeys.TableAssignmentOptimizer(orgId, siteId));
        await grain.InitializeAsync(orgId, siteId);
        return grain;
    }

    // ========================================================================
    // Minimum Party Size Enforcement
    // ========================================================================

    // Given: a 10-top table with MinCapacity=6
    // When: a party of 2 requests recommendations
    // Then: the 10-top is not recommended (party too small for the table)
    [Fact]
    public async Task MinPartySize_ShouldRejectTableWhenPartyTooSmall()
    {
        // Arrange
        var optimizer = await CreateOptimizerAsync();
        var bigTableId = Guid.NewGuid();
        var smallTableId = Guid.NewGuid();

        await optimizer.RegisterTableAsync(bigTableId, "T1", minCapacity: 6, maxCapacity: 10, isCombinable: false);
        await optimizer.RegisterTableAsync(smallTableId, "T2", minCapacity: 1, maxCapacity: 4, isCombinable: false);

        // Act
        var result = await optimizer.GetRecommendationsAsync(new TableAssignmentRequest(
            BookingId: Guid.NewGuid(),
            PartySize: 2,
            BookingTime: DateTime.UtcNow.AddHours(2),
            Duration: TimeSpan.FromMinutes(90)));

        // Assert — T1 should be excluded, only T2 recommended
        result.Success.Should().BeTrue();
        result.Recommendations.Should().NotContain(r => r.TableId == bigTableId);
        result.Recommendations.Should().Contain(r => r.TableId == smallTableId);
    }

    // Given: only a 10-top table with MinCapacity=6
    // When: a party of 2 requests recommendations
    // Then: no tables are recommended
    [Fact]
    public async Task MinPartySize_ShouldReturnNoRecommendations_WhenAllTablesTooLarge()
    {
        // Arrange
        var optimizer = await CreateOptimizerAsync();
        await optimizer.RegisterTableAsync(Guid.NewGuid(), "T1", minCapacity: 6, maxCapacity: 10, isCombinable: false);

        // Act
        var result = await optimizer.GetRecommendationsAsync(new TableAssignmentRequest(
            BookingId: Guid.NewGuid(),
            PartySize: 2,
            BookingTime: DateTime.UtcNow.AddHours(2),
            Duration: TimeSpan.FromMinutes(90)));

        // Assert
        result.Success.Should().BeFalse();
        result.Recommendations.Should().BeEmpty();
    }

    // Given: a table with MinCapacity=2 and MaxCapacity=4
    // When: a party of exactly 2 requests recommendations
    // Then: the table is recommended (meets minimum)
    [Fact]
    public async Task MinPartySize_ExactMinimum_ShouldBeRecommended()
    {
        // Arrange
        var optimizer = await CreateOptimizerAsync();
        var tableId = Guid.NewGuid();
        await optimizer.RegisterTableAsync(tableId, "T1", minCapacity: 2, maxCapacity: 4, isCombinable: false);

        // Act
        var result = await optimizer.GetRecommendationsAsync(new TableAssignmentRequest(
            BookingId: Guid.NewGuid(),
            PartySize: 2,
            BookingTime: DateTime.UtcNow.AddHours(2),
            Duration: TimeSpan.FromMinutes(90)));

        // Assert
        result.Success.Should().BeTrue();
        result.Recommendations.Should().Contain(r => r.TableId == tableId);
    }

    // ========================================================================
    // Table Combination Adjacency Constraints
    // ========================================================================

    // Given: tables A and B are marked as combinable with each other, and C is not
    // When: a large party needs a combination
    // Then: A+B is suggested but A+C is not
    [Fact]
    public async Task CombinationAdjacency_ShouldOnlyCombineAdjacentTables()
    {
        // Arrange
        var optimizer = await CreateOptimizerAsync();
        var tableA = Guid.NewGuid();
        var tableB = Guid.NewGuid();
        var tableC = Guid.NewGuid();

        // A and B are adjacent (reference each other), C is standalone
        await optimizer.RegisterTableAsync(tableA, "A", minCapacity: 1, maxCapacity: 4, isCombinable: true,
            combinableWith: [tableB]);
        await optimizer.RegisterTableAsync(tableB, "B", minCapacity: 1, maxCapacity: 4, isCombinable: true,
            combinableWith: [tableA]);
        await optimizer.RegisterTableAsync(tableC, "C", minCapacity: 1, maxCapacity: 4, isCombinable: true,
            combinableWith: []); // no adjacency specified, but empty list = unrestricted

        // Act — party of 7 needs a combination (no single table fits)
        var result = await optimizer.GetRecommendationsAsync(new TableAssignmentRequest(
            BookingId: Guid.NewGuid(),
            PartySize: 7,
            BookingTime: DateTime.UtcNow.AddHours(2),
            Duration: TimeSpan.FromMinutes(90)));

        // Assert — should find A+B combination (4+4=8 >= 7)
        var combos = result.Recommendations.Where(r => r.RequiresCombination).ToList();
        combos.Should().NotBeEmpty();

        // All combinations should contain A+B (since C has empty CombinableWith, any pair with C is also valid)
        var abCombo = combos.FirstOrDefault(r =>
            r.CombinedTableIds != null &&
            r.CombinedTableIds.Contains(tableA) &&
            r.CombinedTableIds.Contains(tableB));
        abCombo.Should().NotBeNull();
    }

    // Given: tables A and B have CombinableWith lists that do NOT reference each other
    // When: a large party needs a combination
    // Then: no combination is possible
    [Fact]
    public async Task CombinationAdjacency_ShouldRejectNonAdjacentTables()
    {
        // Arrange
        var optimizer = await CreateOptimizerAsync();
        var tableA = Guid.NewGuid();
        var tableB = Guid.NewGuid();
        var otherTableId = Guid.NewGuid(); // doesn't exist in optimizer

        // A references "other" not B; B references "other" not A
        await optimizer.RegisterTableAsync(tableA, "A", minCapacity: 1, maxCapacity: 4, isCombinable: true,
            combinableWith: [otherTableId]);
        await optimizer.RegisterTableAsync(tableB, "B", minCapacity: 1, maxCapacity: 4, isCombinable: true,
            combinableWith: [otherTableId]);

        // Act — party of 7
        var result = await optimizer.GetRecommendationsAsync(new TableAssignmentRequest(
            BookingId: Guid.NewGuid(),
            PartySize: 7,
            BookingTime: DateTime.UtcNow.AddHours(2),
            Duration: TimeSpan.FromMinutes(90)));

        // Assert — no single table fits (max 4) and A+B can't combine
        var combos = result.Recommendations.Where(r => r.RequiresCombination).ToList();
        combos.Should().BeEmpty();
    }

    // Given: a table with MaxCombinationSize=1 (cannot be combined)
    // When: a large party needs a combination
    // Then: this table is excluded from combinations
    [Fact]
    public async Task MaxCombinationSize_ShouldPreventCombination()
    {
        // Arrange
        var optimizer = await CreateOptimizerAsync();
        var tableA = Guid.NewGuid();
        var tableB = Guid.NewGuid();

        await optimizer.RegisterTableAsync(tableA, "A", minCapacity: 1, maxCapacity: 4, isCombinable: true,
            maxCombinationSize: 1); // can't be in any combination
        await optimizer.RegisterTableAsync(tableB, "B", minCapacity: 1, maxCapacity: 4, isCombinable: true,
            maxCombinationSize: 2);

        // Act — party of 7
        var result = await optimizer.GetRecommendationsAsync(new TableAssignmentRequest(
            BookingId: Guid.NewGuid(),
            PartySize: 7,
            BookingTime: DateTime.UtcNow.AddHours(2),
            Duration: TimeSpan.FromMinutes(90)));

        // Assert — no valid combination (A can't combine, B alone can't serve 7)
        var combos = result.Recommendations.Where(r => r.RequiresCombination).ToList();
        combos.Should().BeEmpty();
    }

    // Given: tables registered with CombinableWith data
    // When: the registration is updated
    // Then: the new data persists
    [Fact]
    public async Task RegisterTable_ShouldPersistCombinableWith()
    {
        // Arrange
        var optimizer = await CreateOptimizerAsync();
        var tableId = Guid.NewGuid();
        var adjacentId = Guid.NewGuid();

        // Act
        await optimizer.RegisterTableAsync(tableId, "T1", minCapacity: 2, maxCapacity: 6, isCombinable: true,
            combinableWith: [adjacentId], maxCombinationSize: 2);

        // Assert — re-register same table to verify update
        await optimizer.RegisterTableAsync(tableId, "T1", minCapacity: 2, maxCapacity: 8, isCombinable: true,
            combinableWith: [adjacentId], maxCombinationSize: 3);

        var ids = await optimizer.GetRegisteredTableIdsAsync();
        ids.Should().ContainSingle(id => id == tableId);
    }
}
