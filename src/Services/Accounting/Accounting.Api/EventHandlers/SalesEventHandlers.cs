using DarkVelocity.Accounting.Api.Data;
using DarkVelocity.Accounting.Api.Entities;
using DarkVelocity.Accounting.Api.Services;
using DarkVelocity.Shared.Contracts.Events;
using DarkVelocity.Shared.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DarkVelocity.Accounting.Api.EventHandlers;

/// <summary>
/// Handles OrderCompleted events to create revenue journal entries.
/// When an order is completed, we record:
/// - Debit: Accounts Receivable (the customer owes us)
/// - Credit: Sales Revenue (we've earned income)
/// </summary>
public class OrderCompletedHandler : IEventHandler<OrderCompleted>
{
    private readonly AccountingDbContext _context;
    private readonly IJournalEntryNumberGenerator _entryNumberGenerator;
    private readonly ILogger<OrderCompletedHandler> _logger;

    // Standard account codes - these should match the Chart of Accounts
    private const string ReceivablesAccountCode = "1200";
    private const string ReceivablesAccountName = "Accounts Receivable";
    private const string RevenueAccountCode = "4000";
    private const string RevenueAccountName = "Sales Revenue";

    public OrderCompletedHandler(
        AccountingDbContext context,
        IJournalEntryNumberGenerator entryNumberGenerator,
        ILogger<OrderCompletedHandler> logger)
    {
        _context = context;
        _entryNumberGenerator = entryNumberGenerator;
        _logger = logger;
    }

    public async Task HandleAsync(OrderCompleted @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handling OrderCompleted event for order {OrderId} at location {LocationId}, total: {GrandTotal}",
            @event.OrderId,
            @event.LocationId,
            @event.GrandTotal);

        // Check if we already processed this event (idempotency)
        var existingEntry = await _context.JournalEntries
            .FirstOrDefaultAsync(e =>
                e.SourceType == JournalEntrySourceType.Order &&
                e.SourceId == @event.OrderId,
                cancellationToken);

        if (existingEntry != null)
        {
            _logger.LogDebug(
                "Journal entry already exists for order {OrderId}, skipping",
                @event.OrderId);
            return;
        }

        // Get or determine TenantId (in production, this would come from the event or context)
        // For now, we'll look up an existing account to get the TenantId
        var existingAccount = await _context.Accounts
            .FirstOrDefaultAsync(cancellationToken);

        if (existingAccount == null)
        {
            _logger.LogWarning(
                "No accounts found in the system, cannot create journal entry for order {OrderId}",
                @event.OrderId);
            return;
        }

        var tenantId = existingAccount.TenantId;

        // Get or create the required accounts
        var receivablesAccount = await GetOrCreateAccountAsync(
            tenantId,
            ReceivablesAccountCode,
            ReceivablesAccountName,
            AccountType.Asset,
            "Receivables",
            cancellationToken);

        var revenueAccount = await GetOrCreateAccountAsync(
            tenantId,
            RevenueAccountCode,
            RevenueAccountName,
            AccountType.Revenue,
            "Sales",
            cancellationToken);

        // Generate journal entry number
        var entryNumber = await _entryNumberGenerator.GenerateAsync(tenantId, @event.LocationId);

        // Create the journal entry
        var journalEntry = new JournalEntry
        {
            TenantId = tenantId,
            LocationId = @event.LocationId,
            EntryNumber = entryNumber,
            EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            PostedAt = DateTime.UtcNow,
            SourceType = JournalEntrySourceType.Order,
            SourceId = @event.OrderId,
            Description = $"Sales revenue for order {@event.OrderNumber}",
            TotalDebit = @event.GrandTotal,
            TotalCredit = @event.GrandTotal,
            Currency = "EUR",
            Status = JournalEntryStatus.Posted
        };

        // Add journal entry lines
        // Line 1: Debit Accounts Receivable
        journalEntry.Lines.Add(new JournalEntryLine
        {
            JournalEntryId = journalEntry.Id,
            AccountCode = receivablesAccount.AccountCode,
            AccountName = receivablesAccount.Name,
            DebitAmount = @event.GrandTotal,
            CreditAmount = 0,
            LineNumber = 1,
            Description = $"Receivable for order {@event.OrderNumber}"
        });

        // Line 2: Credit Sales Revenue
        journalEntry.Lines.Add(new JournalEntryLine
        {
            JournalEntryId = journalEntry.Id,
            AccountCode = revenueAccount.AccountCode,
            AccountName = revenueAccount.Name,
            DebitAmount = 0,
            CreditAmount = @event.GrandTotal,
            LineNumber = 2,
            Description = $"Revenue for order {@event.OrderNumber}"
        });

        _context.JournalEntries.Add(journalEntry);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created journal entry {EntryNumber} for order {OrderId} with total {GrandTotal}",
            entryNumber,
            @event.OrderId,
            @event.GrandTotal);
    }

    private async Task<Account> GetOrCreateAccountAsync(
        Guid tenantId,
        string accountCode,
        string accountName,
        AccountType accountType,
        string subType,
        CancellationToken cancellationToken)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a =>
                a.TenantId == tenantId &&
                a.AccountCode == accountCode,
                cancellationToken);

        if (account != null)
        {
            return account;
        }

        // Create the account if it doesn't exist
        account = new Account
        {
            TenantId = tenantId,
            AccountCode = accountCode,
            Name = accountName,
            AccountType = accountType,
            SubType = subType,
            IsSystemAccount = true,
            IsActive = true
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created system account {AccountCode} - {AccountName}",
            accountCode,
            accountName);

        return account;
    }
}

/// <summary>
/// Handles PaymentCompleted events to settle receivables.
/// When a payment is completed, we record:
/// - Debit: Cash (we received money)
/// - Credit: Accounts Receivable (the debt is paid)
/// </summary>
public class PaymentCompletedHandler : IEventHandler<PaymentCompleted>
{
    private readonly AccountingDbContext _context;
    private readonly IJournalEntryNumberGenerator _entryNumberGenerator;
    private readonly ILogger<PaymentCompletedHandler> _logger;

    // Standard account codes
    private const string CashAccountCode = "1000";
    private const string CashAccountName = "Cash";
    private const string ReceivablesAccountCode = "1200";
    private const string ReceivablesAccountName = "Accounts Receivable";

    public PaymentCompletedHandler(
        AccountingDbContext context,
        IJournalEntryNumberGenerator entryNumberGenerator,
        ILogger<PaymentCompletedHandler> logger)
    {
        _context = context;
        _entryNumberGenerator = entryNumberGenerator;
        _logger = logger;
    }

    public async Task HandleAsync(PaymentCompleted @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handling PaymentCompleted event for payment {PaymentId}, order {OrderId}, amount: {Amount}",
            @event.PaymentId,
            @event.OrderId,
            @event.Amount);

        // Check if we already processed this event (idempotency)
        var existingEntry = await _context.JournalEntries
            .FirstOrDefaultAsync(e =>
                e.SourceType == JournalEntrySourceType.Payment &&
                e.SourceId == @event.PaymentId,
                cancellationToken);

        if (existingEntry != null)
        {
            _logger.LogDebug(
                "Journal entry already exists for payment {PaymentId}, skipping",
                @event.PaymentId);
            return;
        }

        // Get TenantId from existing account
        var existingAccount = await _context.Accounts
            .FirstOrDefaultAsync(cancellationToken);

        if (existingAccount == null)
        {
            _logger.LogWarning(
                "No accounts found in the system, cannot create journal entry for payment {PaymentId}",
                @event.PaymentId);
            return;
        }

        var tenantId = existingAccount.TenantId;

        // Get or create the required accounts
        var cashAccount = await GetOrCreateAccountAsync(
            tenantId,
            CashAccountCode,
            CashAccountName,
            AccountType.Asset,
            "Cash",
            cancellationToken);

        var receivablesAccount = await GetOrCreateAccountAsync(
            tenantId,
            ReceivablesAccountCode,
            ReceivablesAccountName,
            AccountType.Asset,
            "Receivables",
            cancellationToken);

        // Generate journal entry number
        var entryNumber = await _entryNumberGenerator.GenerateAsync(tenantId, @event.LocationId);

        // Create the journal entry
        var journalEntry = new JournalEntry
        {
            TenantId = tenantId,
            LocationId = @event.LocationId,
            EntryNumber = entryNumber,
            EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            PostedAt = DateTime.UtcNow,
            SourceType = JournalEntrySourceType.Payment,
            SourceId = @event.PaymentId,
            Description = $"Payment received via {@event.PaymentMethod}",
            TotalDebit = @event.Amount,
            TotalCredit = @event.Amount,
            Currency = @event.Currency,
            Status = JournalEntryStatus.Posted
        };

        // Add journal entry lines
        // Line 1: Debit Cash
        journalEntry.Lines.Add(new JournalEntryLine
        {
            JournalEntryId = journalEntry.Id,
            AccountCode = cashAccount.AccountCode,
            AccountName = cashAccount.Name,
            DebitAmount = @event.Amount,
            CreditAmount = 0,
            LineNumber = 1,
            Description = $"Payment received - {@event.PaymentMethod}"
        });

        // Line 2: Credit Accounts Receivable
        journalEntry.Lines.Add(new JournalEntryLine
        {
            JournalEntryId = journalEntry.Id,
            AccountCode = receivablesAccount.AccountCode,
            AccountName = receivablesAccount.Name,
            DebitAmount = 0,
            CreditAmount = @event.Amount,
            LineNumber = 2,
            Description = $"Receivable settled for order {@event.OrderId}"
        });

        _context.JournalEntries.Add(journalEntry);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created journal entry {EntryNumber} for payment {PaymentId} with amount {Amount}",
            entryNumber,
            @event.PaymentId,
            @event.Amount);
    }

    private async Task<Account> GetOrCreateAccountAsync(
        Guid tenantId,
        string accountCode,
        string accountName,
        AccountType accountType,
        string subType,
        CancellationToken cancellationToken)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a =>
                a.TenantId == tenantId &&
                a.AccountCode == accountCode,
                cancellationToken);

        if (account != null)
        {
            return account;
        }

        // Create the account if it doesn't exist
        account = new Account
        {
            TenantId = tenantId,
            AccountCode = accountCode,
            Name = accountName,
            AccountType = accountType,
            SubType = subType,
            IsSystemAccount = true,
            IsActive = true
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created system account {AccountCode} - {AccountName}",
            accountCode,
            accountName);

        return account;
    }
}
