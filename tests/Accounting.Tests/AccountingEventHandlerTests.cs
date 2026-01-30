using DarkVelocity.Accounting.Api.Entities;
using DarkVelocity.Shared.Contracts.Events;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Accounting.Tests;

public class AccountingEventHandlerTests : IClassFixture<AccountingApiFixture>
{
    private readonly AccountingApiFixture _fixture;

    public AccountingEventHandlerTests(AccountingApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task OrderCompletedHandler_CreatesRevenueJournalEntry()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var orderNumber = "20260130-0001";
        var grandTotal = 50.00m;

        var orderCompletedEvent = new OrderCompleted(
            OrderId: orderId,
            LocationId: _fixture.TestLocationId,
            OrderNumber: orderNumber,
            GrandTotal: grandTotal,
            Lines: new List<OrderLineSnapshot>
            {
                new OrderLineSnapshot(
                    LineId: Guid.NewGuid(),
                    MenuItemId: Guid.NewGuid(),
                    ItemName: "Burger",
                    Quantity: 2,
                    UnitPrice: 15.00m,
                    LineTotal: 30.00m),
                new OrderLineSnapshot(
                    LineId: Guid.NewGuid(),
                    MenuItemId: Guid.NewGuid(),
                    ItemName: "Fries",
                    Quantity: 2,
                    UnitPrice: 5.00m,
                    LineTotal: 10.00m),
                new OrderLineSnapshot(
                    LineId: Guid.NewGuid(),
                    MenuItemId: Guid.NewGuid(),
                    ItemName: "Drink",
                    Quantity: 2,
                    UnitPrice: 5.00m,
                    LineTotal: 10.00m)
            });

        var eventBus = _fixture.GetEventBus();

        // Act
        await eventBus.PublishAsync(orderCompletedEvent);

        // Assert
        using var db = _fixture.GetDbContext();

        var journalEntry = await db.JournalEntries
            .Include(j => j.Lines)
            .FirstOrDefaultAsync(j => j.SourceId == orderId && j.SourceType == JournalEntrySourceType.Order);

        journalEntry.Should().NotBeNull();
        journalEntry!.LocationId.Should().Be(_fixture.TestLocationId);
        journalEntry.TotalDebit.Should().Be(grandTotal);
        journalEntry.TotalCredit.Should().Be(grandTotal);
        journalEntry.Description.Should().Contain(orderNumber);
        journalEntry.Status.Should().Be(JournalEntryStatus.Posted);

        // Verify the entry has balanced debits and credits
        journalEntry.Lines.Should().HaveCount(2);

        var debitLine = journalEntry.Lines.FirstOrDefault(l => l.DebitAmount > 0);
        var creditLine = journalEntry.Lines.FirstOrDefault(l => l.CreditAmount > 0);

        debitLine.Should().NotBeNull();
        debitLine!.AccountCode.Should().Be("1200"); // Accounts Receivable
        debitLine.DebitAmount.Should().Be(grandTotal);

        creditLine.Should().NotBeNull();
        creditLine!.AccountCode.Should().Be("4000"); // Sales Revenue
        creditLine.CreditAmount.Should().Be(grandTotal);
    }

    [Fact]
    public async Task OrderCompletedHandler_IsIdempotent_DoesNotCreateDuplicateEntries()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var orderNumber = "20260130-0002";
        var grandTotal = 25.00m;

        var orderCompletedEvent = new OrderCompleted(
            OrderId: orderId,
            LocationId: _fixture.TestLocationId,
            OrderNumber: orderNumber,
            GrandTotal: grandTotal,
            Lines: new List<OrderLineSnapshot>
            {
                new OrderLineSnapshot(
                    LineId: Guid.NewGuid(),
                    MenuItemId: Guid.NewGuid(),
                    ItemName: "Pizza",
                    Quantity: 1,
                    UnitPrice: 25.00m,
                    LineTotal: 25.00m)
            });

        var eventBus = _fixture.GetEventBus();

        // Act - publish the same event twice
        await eventBus.PublishAsync(orderCompletedEvent);
        await eventBus.PublishAsync(orderCompletedEvent);

        // Assert - only one journal entry should exist
        using var db = _fixture.GetDbContext();

        var entries = await db.JournalEntries
            .Where(j => j.SourceId == orderId && j.SourceType == JournalEntrySourceType.Order)
            .ToListAsync();

        entries.Should().HaveCount(1);
    }

    [Fact]
    public async Task PaymentCompletedHandler_SettlesReceivables()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var amount = 75.00m;

        var paymentCompletedEvent = new PaymentCompleted(
            PaymentId: paymentId,
            OrderId: orderId,
            LocationId: _fixture.TestLocationId,
            Amount: amount,
            PaymentMethod: "card",
            Currency: "EUR",
            TransactionReference: "TXN-123456");

        var eventBus = _fixture.GetEventBus();

        // Act
        await eventBus.PublishAsync(paymentCompletedEvent);

        // Assert
        using var db = _fixture.GetDbContext();

        var journalEntry = await db.JournalEntries
            .Include(j => j.Lines)
            .FirstOrDefaultAsync(j => j.SourceId == paymentId && j.SourceType == JournalEntrySourceType.Payment);

        journalEntry.Should().NotBeNull();
        journalEntry!.LocationId.Should().Be(_fixture.TestLocationId);
        journalEntry.TotalDebit.Should().Be(amount);
        journalEntry.TotalCredit.Should().Be(amount);
        journalEntry.Description.Should().Contain("card");
        journalEntry.Currency.Should().Be("EUR");
        journalEntry.Status.Should().Be(JournalEntryStatus.Posted);

        // Verify the entry has balanced debits and credits
        journalEntry.Lines.Should().HaveCount(2);

        var debitLine = journalEntry.Lines.FirstOrDefault(l => l.DebitAmount > 0);
        var creditLine = journalEntry.Lines.FirstOrDefault(l => l.CreditAmount > 0);

        debitLine.Should().NotBeNull();
        debitLine!.AccountCode.Should().Be("1000"); // Cash
        debitLine.DebitAmount.Should().Be(amount);

        creditLine.Should().NotBeNull();
        creditLine!.AccountCode.Should().Be("1200"); // Accounts Receivable
        creditLine.CreditAmount.Should().Be(amount);
    }

    [Fact]
    public async Task PaymentCompletedHandler_IsIdempotent_DoesNotCreateDuplicateEntries()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var amount = 100.00m;

        var paymentCompletedEvent = new PaymentCompleted(
            PaymentId: paymentId,
            OrderId: orderId,
            LocationId: _fixture.TestLocationId,
            Amount: amount,
            PaymentMethod: "cash",
            Currency: "EUR",
            TransactionReference: null);

        var eventBus = _fixture.GetEventBus();

        // Act - publish the same event twice
        await eventBus.PublishAsync(paymentCompletedEvent);
        await eventBus.PublishAsync(paymentCompletedEvent);

        // Assert - only one journal entry should exist
        using var db = _fixture.GetDbContext();

        var entries = await db.JournalEntries
            .Where(j => j.SourceId == paymentId && j.SourceType == JournalEntrySourceType.Payment)
            .ToListAsync();

        entries.Should().HaveCount(1);
    }

    [Fact]
    public async Task OrderCompletedHandler_CreatesRequiredAccounts_WhenMissing()
    {
        // Arrange - use a unique order to test account creation
        var orderId = Guid.NewGuid();
        var orderNumber = "20260130-0003";
        var grandTotal = 35.00m;

        var orderCompletedEvent = new OrderCompleted(
            OrderId: orderId,
            LocationId: _fixture.TestLocationId,
            OrderNumber: orderNumber,
            GrandTotal: grandTotal,
            Lines: new List<OrderLineSnapshot>
            {
                new OrderLineSnapshot(
                    LineId: Guid.NewGuid(),
                    MenuItemId: Guid.NewGuid(),
                    ItemName: "Salad",
                    Quantity: 1,
                    UnitPrice: 35.00m,
                    LineTotal: 35.00m)
            });

        var eventBus = _fixture.GetEventBus();

        // Act
        await eventBus.PublishAsync(orderCompletedEvent);

        // Assert - verify accounts exist
        using var db = _fixture.GetDbContext();

        var receivablesAccount = await db.Accounts
            .FirstOrDefaultAsync(a => a.AccountCode == "1200");
        var revenueAccount = await db.Accounts
            .FirstOrDefaultAsync(a => a.AccountCode == "4000");

        receivablesAccount.Should().NotBeNull();
        receivablesAccount!.Name.Should().Be("Accounts Receivable");
        receivablesAccount.AccountType.Should().Be(AccountType.Asset);
        receivablesAccount.IsSystemAccount.Should().BeTrue();

        revenueAccount.Should().NotBeNull();
        revenueAccount!.Name.Should().Be("Sales Revenue");
        revenueAccount.AccountType.Should().Be(AccountType.Revenue);
        revenueAccount.IsSystemAccount.Should().BeTrue();
    }

    [Fact]
    public async Task JournalEntry_TrialBalance_RemainsBalanced()
    {
        // Arrange - create order and payment events
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var amount = 60.00m;

        var orderEvent = new OrderCompleted(
            OrderId: orderId,
            LocationId: _fixture.TestLocationId,
            OrderNumber: "20260130-0004",
            GrandTotal: amount,
            Lines: new List<OrderLineSnapshot>
            {
                new OrderLineSnapshot(
                    LineId: Guid.NewGuid(),
                    MenuItemId: Guid.NewGuid(),
                    ItemName: "Steak",
                    Quantity: 1,
                    UnitPrice: 60.00m,
                    LineTotal: 60.00m)
            });

        var paymentEvent = new PaymentCompleted(
            PaymentId: paymentId,
            OrderId: orderId,
            LocationId: _fixture.TestLocationId,
            Amount: amount,
            PaymentMethod: "card",
            Currency: "EUR",
            TransactionReference: "TXN-789");

        var eventBus = _fixture.GetEventBus();

        // Act
        await eventBus.PublishAsync(orderEvent);
        await eventBus.PublishAsync(paymentEvent);

        // Assert - verify trial balance is balanced
        using var db = _fixture.GetDbContext();

        var allLines = await db.JournalEntryLines.ToListAsync();
        var totalDebits = allLines.Sum(l => l.DebitAmount);
        var totalCredits = allLines.Sum(l => l.CreditAmount);

        totalDebits.Should().Be(totalCredits);
    }
}
