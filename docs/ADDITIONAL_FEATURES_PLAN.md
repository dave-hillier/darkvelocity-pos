# DarkVelocity POS - Additional Features Plan

This document outlines the implementation plan for additional core features required to make DarkVelocity POS a complete, production-ready system for European markets with multi-tenant SaaS deployment.

---

## Table of Contents

1. [Gift Cards Service](#1-gift-cards-service)
2. [Fiscalisation Service](#2-fiscalisation-service)
3. [Accounting Service](#3-accounting-service)
4. [Multi-Tenancy Architecture](#4-multi-tenancy-architecture)
5. [Kubernetes-Native Deployment](#5-kubernetes-native-deployment-no-api-gateway)
6. [Implementation Phases](#6-implementation-phases)

---

## 1. Gift Cards Service

A dedicated microservice for managing gift cards, vouchers, and stored value products.

### Service Overview

| Attribute | Value |
|-----------|-------|
| Service Name | GiftCards.Api |
| Port | 5012 |
| Database | giftcards_db |
| Dependencies | Payments, Orders, Auth |

### Core Entities

```
GiftCard
├── Id (Guid)
├── CardNumber (string, unique, 16-19 digits)
├── Pin (string, hashed, 4-8 digits)
├── InitialBalance (decimal)
├── CurrentBalance (decimal)
├── Currency (string, ISO 4217)
├── Status (Active, Suspended, Expired, Depleted)
├── CardType (Physical, Digital, Promotional)
├── ExpiryDate (DateTime?)
├── IssuedAt (DateTime)
├── IssuedByLocationId (Guid)
├── IssuedByUserId (Guid)
├── TenantId (Guid)
├── LastUsedAt (DateTime?)
└── Metadata (JSON - custom fields)

GiftCardTransaction
├── Id (Guid)
├── GiftCardId (Guid)
├── TransactionType (Activation, Redemption, Reload, Adjustment, Refund)
├── Amount (decimal)
├── BalanceBefore (decimal)
├── BalanceAfter (decimal)
├── OrderId (Guid?)
├── LocationId (Guid)
├── UserId (Guid)
├── Reason (string?)
├── ProcessedAt (DateTime)
└── ExternalReference (string?) - for fiscal linkage

GiftCardProgram
├── Id (Guid)
├── TenantId (Guid)
├── Name (string)
├── CardNumberPrefix (string)
├── DefaultExpiryMonths (int?)
├── MinimumLoadAmount (decimal)
├── MaximumLoadAmount (decimal)
├── MaximumBalance (decimal)
├── AllowReload (bool)
├── AllowPartialRedemption (bool)
├── RequirePin (bool)
└── IsActive (bool)

GiftCardDesign
├── Id (Guid)
├── ProgramId (Guid)
├── Name (string)
├── ImageUrl (string)
├── IsDefault (bool)
└── IsActive (bool)
```

### API Endpoints (HAL+JSON)

```
Gift Card Management
├── GET    /api/giftcards                           # List cards (with filters)
├── POST   /api/giftcards                           # Create/issue new card
├── GET    /api/giftcards/{id}                      # Get card details
├── GET    /api/giftcards/lookup?number={number}    # Lookup by card number
├── POST   /api/giftcards/{id}/activate             # Activate card
├── POST   /api/giftcards/{id}/suspend              # Suspend card
├── POST   /api/giftcards/{id}/resume               # Resume suspended card
├── POST   /api/giftcards/{id}/reload               # Add balance
├── POST   /api/giftcards/{id}/redeem               # Deduct balance (payment)
├── POST   /api/giftcards/{id}/refund               # Refund to card
├── POST   /api/giftcards/{id}/adjust               # Manual adjustment
├── GET    /api/giftcards/{id}/transactions         # Transaction history
└── GET    /api/giftcards/{id}/balance              # Quick balance check

Programs
├── GET    /api/giftcard-programs                   # List programs
├── POST   /api/giftcard-programs                   # Create program
├── GET    /api/giftcard-programs/{id}              # Get program
├── PUT    /api/giftcard-programs/{id}              # Update program
└── GET    /api/giftcard-programs/{id}/cards        # Cards in program

Designs
├── GET    /api/giftcard-programs/{id}/designs      # List designs
├── POST   /api/giftcard-programs/{id}/designs      # Create design
└── PUT    /api/giftcard-designs/{id}               # Update design

Reporting
├── GET    /api/giftcard-reports/liability          # Outstanding liability
├── GET    /api/giftcard-reports/activity           # Activity summary
└── GET    /api/giftcard-reports/expiring           # Cards expiring soon
```

### Integration Points

**With Payments Service:**
- Gift card as payment method
- Partial payment with gift card + other method
- Refunds to gift card

**With Orders Service:**
- Link redemption to order
- Purchase gift cards as line items

**With Fiscalisation Service:**
- All transactions require fiscal recording
- Gift card liability tracking for tax purposes

### Kafka Events

```
GiftCardIssued
├── CardId, CardNumber, InitialBalance, IssuedAt, LocationId

GiftCardActivated
├── CardId, ActivatedAt, LocationId, UserId

GiftCardRedeemed
├── CardId, Amount, OrderId, BalanceAfter, RedeemedAt

GiftCardReloaded
├── CardId, Amount, BalanceAfter, ReloadedAt

GiftCardExpired
├── CardId, ExpiredAt, FinalBalance

GiftCardBalanceAdjusted
├── CardId, Amount, Reason, AdjustedAt, UserId
```

---

## 2. Fiscalisation Service

A critical service for European tax compliance, specifically designed for German KassenSichV (TSE) requirements but extensible to other jurisdictions.

### Service Overview

| Attribute | Value |
|-----------|-------|
| Service Name | Fiscalisation.Api |
| Port | 5013 |
| Database | fiscalisation_db |
| Dependencies | Orders, Payments, GiftCards, Auth |

### Regulatory Requirements

#### KassenSichV (Germany)
- Technical Security Equipment (TSE) integration
- Tamper-proof transaction logging
- Digital signatures for all transactions
- DSFinV-K export format
- QR code on receipts for verification

#### Other European Requirements (Future)
- Austria: RKSV (Registrierkassensicherheitsverordnung)
- France: NF 525 certification
- Italy: RT (Registratore Telematico)
- Poland: Centralne Repozytorium Kas

### Core Entities

```
FiscalDevice (TSE)
├── Id (Guid)
├── TenantId (Guid)
├── LocationId (Guid)
├── DeviceType (SwissbitCloud, SwissbitUSB, FiskalyCloud, Epson, Diebold)
├── SerialNumber (string)
├── PublicKey (string)
├── CertificateExpiryDate (DateTime)
├── Status (Active, Inactive, Failed, CertificateExpiring)
├── ApiEndpoint (string?) - for cloud TSE
├── ApiCredentialsEncrypted (string?)
├── LastSyncAt (DateTime?)
├── TransactionCounter (long)
└── SignatureCounter (long)

FiscalTransaction
├── Id (Guid)
├── FiscalDeviceId (Guid)
├── TransactionNumber (long) - sequential per device
├── TransactionType (Receipt, TrainingReceipt, Void, Cancellation)
├── ProcessType (Kassenbeleg, AVTransfer, AVBestellung, AVSonstiger)
├── StartTime (DateTime)
├── EndTime (DateTime)
├── SourceType (Order, Payment, GiftCard, CashDrawer)
├── SourceId (Guid)
├── GrossAmount (decimal)
├── NetAmounts (JSON) - per tax rate
├── TaxAmounts (JSON) - per tax rate
├── PaymentTypes (JSON) - breakdown
├── Signature (string)
├── SignatureCounter (long)
├── CertificateSerial (string)
├── QRCodeData (string)
├── TseResponseRaw (string) - full TSE response
├── Status (Pending, Signed, Failed, Retrying)
├── ErrorMessage (string?)
├── RetryCount (int)
└── ExportedAt (DateTime?)

FiscalExport (DSFinV-K)
├── Id (Guid)
├── TenantId (Guid)
├── LocationId (Guid)
├── ExportType (Daily, Monthly, OnDemand, AuditRequest)
├── StartDate (Date)
├── EndDate (Date)
├── Status (Generating, Completed, Failed)
├── FileUrl (string)
├── FileSha256 (string)
├── TransactionCount (int)
├── GeneratedAt (DateTime)
├── RequestedByUserId (Guid)
└── AuditReference (string?) - for tax authority requests

FiscalJournal (Audit Log)
├── Id (Guid)
├── TenantId (Guid)
├── Timestamp (DateTime)
├── EventType (TransactionSigned, DeviceRegistered, ExportGenerated, Error)
├── DeviceId (Guid?)
├── TransactionId (Guid?)
├── Details (JSON)
├── IpAddress (string)
└── UserId (Guid?)

TaxRate
├── Id (Guid)
├── TenantId (Guid)
├── CountryCode (string)
├── Rate (decimal)
├── FiscalCode (string) - e.g., "A" for 19%, "B" for 7% in Germany
├── Description (string)
├── EffectiveFrom (Date)
├── EffectiveTo (Date?)
└── IsActive (bool)
```

### API Endpoints

```
Fiscal Devices (TSE)
├── GET    /api/fiscal-devices                      # List devices
├── POST   /api/fiscal-devices                      # Register new TSE
├── GET    /api/fiscal-devices/{id}                 # Get device info
├── POST   /api/fiscal-devices/{id}/initialize      # Initialize TSE
├── POST   /api/fiscal-devices/{id}/self-test       # Run self-test
├── GET    /api/fiscal-devices/{id}/status          # Health status
└── POST   /api/fiscal-devices/{id}/decommission    # Decommission device

Transactions
├── POST   /api/fiscal-transactions                 # Create & sign transaction
├── GET    /api/fiscal-transactions/{id}            # Get transaction
├── GET    /api/fiscal-transactions/{id}/qr         # Get QR code data
├── POST   /api/fiscal-transactions/{id}/void       # Void transaction
└── GET    /api/locations/{locId}/fiscal-transactions  # By location

Exports (DSFinV-K)
├── POST   /api/fiscal-exports                      # Generate export
├── GET    /api/fiscal-exports/{id}                 # Get export status
├── GET    /api/fiscal-exports/{id}/download        # Download export file
└── GET    /api/fiscal-exports                      # List exports

Tax Rates
├── GET    /api/tax-rates                           # List tax rates
├── POST   /api/tax-rates                           # Create tax rate
└── PUT    /api/tax-rates/{id}                      # Update tax rate

Journal (Audit)
├── GET    /api/fiscal-journal                      # Query audit log
└── GET    /api/fiscal-journal/export               # Export audit log
```

### TSE Integration Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Fiscalisation Service                         │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────────┐    ┌─────────────────┐    ┌─────────────┐ │
│  │  TSE Adapter    │    │  TSE Adapter    │    │ TSE Adapter │ │
│  │  (Swissbit)     │    │  (Fiskaly)      │    │ (Epson)     │ │
│  └────────┬────────┘    └────────┬────────┘    └──────┬──────┘ │
└───────────┼─────────────────────┼────────────────────┼──────────┘
            │                     │                    │
            ▼                     ▼                    ▼
    ┌───────────────┐    ┌───────────────────┐   ┌───────────────┐
    │ Swissbit      │    │ Fiskaly Cloud     │   │ Epson TSE     │
    │ Cloud/USB     │    │ (German TSE       │   │ Hardware      │
    │               │    │  as-a-Service)    │   │               │
    └───────────────┘    └───────────────────┘   └───────────────┘
```

### Receipt QR Code Format (KassenSichV)

```
V0;[TSE-Seriennummer];[Signaturzähler];[Start-Zeit];[Ende-Zeit];
[Prozess-Typ];[Prozess-Daten];[Brutto-Umsätze];[Signatur-Base64]
```

### DSFinV-K Export Structure

```
DSFinV-K Export (ZIP)
├── single_file/
│   ├── cashpointclosing.csv     # Z-Abschlüsse
│   ├── transactions.csv          # All transactions
│   ├── transactions_tse.csv      # TSE data per transaction
│   ├── payment.csv               # Payment details
│   ├── lines.csv                 # Line items
│   ├── lines_vat.csv             # VAT breakdown
│   └── cash_per_currency.csv     # Cash movements
├── index.xml                      # Index file
├── processing_protocols/          # Processing protocols
└── other_obligatory_data/         # Additional required data
```

### Kafka Events

```
TransactionSigned
├── TransactionId, DeviceId, Signature, SignatureCounter, Timestamp

TransactionSigningFailed
├── TransactionId, DeviceId, ErrorCode, ErrorMessage, WillRetry

FiscalDeviceHealthChanged
├── DeviceId, OldStatus, NewStatus, Reason

ExportGenerated
├── ExportId, LocationId, StartDate, EndDate, TransactionCount
```

---

## 3. Accounting Service

A core service that bridges the gap between operational data (tabs, orders, payments) and merchant accounting systems. Acts as the single source of truth for financial records.

### Service Overview

| Attribute | Value |
|-----------|-------|
| Service Name | Accounting.Api |
| Port | 5014 |
| Database | accounting_db |
| Dependencies | Orders, Payments, GiftCards, Fiscalisation, Inventory |

### Core Entities

```
AccountingPeriod
├── Id (Guid)
├── TenantId (Guid)
├── LocationId (Guid)
├── PeriodType (Daily, Weekly, Monthly)
├── StartDate (Date)
├── EndDate (Date)
├── Status (Open, Closing, Closed, Locked)
├── ClosedAt (DateTime?)
├── ClosedByUserId (Guid?)
└── Notes (string?)

JournalEntry
├── Id (Guid)
├── TenantId (Guid)
├── LocationId (Guid)
├── EntryNumber (string) - sequential
├── EntryDate (Date)
├── PostedAt (DateTime)
├── SourceType (Order, Payment, CashMovement, Adjustment, GiftCard)
├── SourceId (Guid)
├── Description (string)
├── TotalDebit (decimal)
├── TotalCredit (decimal)
├── Currency (string)
├── Status (Pending, Posted, Reversed)
├── ReversedByEntryId (Guid?)
├── FiscalTransactionId (Guid?)
└── Lines → JournalEntryLine[]

JournalEntryLine
├── Id (Guid)
├── JournalEntryId (Guid)
├── AccountCode (string)
├── AccountName (string)
├── DebitAmount (decimal)
├── CreditAmount (decimal)
├── TaxCode (string?)
├── TaxAmount (decimal?)
├── CostCenterId (Guid?)
└── Description (string?)

Account (Chart of Accounts)
├── Id (Guid)
├── TenantId (Guid)
├── AccountCode (string)
├── Name (string)
├── AccountType (Asset, Liability, Equity, Revenue, Expense)
├── SubType (string) - e.g., "Cash", "Receivables", "Sales"
├── ParentAccountId (Guid?)
├── IsSystemAccount (bool) - auto-created, cannot delete
├── IsActive (bool)
├── TaxCode (string?)
└── ExternalReference (string?) - for ERP mapping

CostCenter
├── Id (Guid)
├── TenantId (Guid)
├── Code (string)
├── Name (string)
├── LocationId (Guid?)
└── IsActive (bool)

Reconciliation
├── Id (Guid)
├── TenantId (Guid)
├── LocationId (Guid)
├── ReconciliationType (CashDrawer, BankDeposit, CardSettlement)
├── Date (Date)
├── ExpectedAmount (decimal)
├── ActualAmount (decimal)
├── Variance (decimal)
├── Status (Pending, Matched, Variance, Investigated, Resolved)
├── ResolvedAt (DateTime?)
├── ResolvedByUserId (Guid?)
├── ResolutionNotes (string?)
└── RelatedEntryIds (Guid[])

TaxLiability
├── Id (Guid)
├── TenantId (Guid)
├── Period (string) - e.g., "2026-Q1"
├── TaxCode (string)
├── TaxRate (decimal)
├── TaxableAmount (decimal)
├── TaxAmount (decimal)
├── Status (Calculated, Filed, Paid)
└── FiledAt (DateTime?)

GiftCardLiability
├── Id (Guid)
├── TenantId (Guid)
├── AsOfDate (Date)
├── TotalOutstandingCards (int)
├── TotalLiability (decimal)
├── BreakdownByProgram (JSON)
├── BreakdownByAge (JSON) - 0-30, 30-60, 60-90, 90+ days
└── CalculatedAt (DateTime)
```

### Automatic Journal Entry Generation

The service automatically creates journal entries from operational events:

```
Order Completed → Journal Entry
├── DR: Accounts Receivable (or Cash/Card)    $12.00
├── CR: Sales Revenue                         $10.09
└── CR: VAT Payable                           $ 1.91

Payment Received (Cash) → Journal Entry
├── DR: Cash in Drawer                        $12.00
└── CR: Accounts Receivable                   $12.00

Gift Card Issued → Journal Entry
├── DR: Cash/Card                             $50.00
└── CR: Gift Card Liability                   $50.00

Gift Card Redeemed → Journal Entry
├── DR: Gift Card Liability                   $25.00
└── CR: Sales Revenue                         $21.01
└── CR: VAT Payable                           $ 3.99

Inventory Received → Journal Entry
├── DR: Inventory Asset                       $500.00
└── CR: Accounts Payable                      $500.00

Cost of Goods Sold → Journal Entry
├── DR: COGS Expense                          $4.50
└── CR: Inventory Asset                       $4.50
```

### API Endpoints

```
Chart of Accounts
├── GET    /api/accounts                            # List accounts
├── POST   /api/accounts                            # Create account
├── GET    /api/accounts/{id}                       # Get account
├── PUT    /api/accounts/{id}                       # Update account
├── GET    /api/accounts/tree                       # Hierarchical view
└── POST   /api/accounts/import                     # Import from template

Journal Entries
├── GET    /api/journal-entries                     # List entries (filtered)
├── POST   /api/journal-entries                     # Manual entry
├── GET    /api/journal-entries/{id}                # Get entry with lines
├── POST   /api/journal-entries/{id}/reverse        # Reverse entry
└── GET    /api/journal-entries/by-source/{type}/{id}  # Find by source

Accounting Periods
├── GET    /api/accounting-periods                  # List periods
├── POST   /api/accounting-periods                  # Open new period
├── GET    /api/accounting-periods/{id}             # Get period
├── POST   /api/accounting-periods/{id}/close       # Close period
├── POST   /api/accounting-periods/{id}/lock        # Lock (prevent changes)
└── GET    /api/accounting-periods/current          # Get current period

Reconciliation
├── GET    /api/reconciliations                     # List reconciliations
├── POST   /api/reconciliations                     # Start reconciliation
├── PUT    /api/reconciliations/{id}                # Update amounts
├── POST   /api/reconciliations/{id}/resolve        # Resolve variance
└── GET    /api/reconciliations/pending             # Pending items

Financial Reports
├── GET    /api/reports/trial-balance               # Trial balance
├── GET    /api/reports/profit-loss                 # P&L statement
├── GET    /api/reports/balance-sheet               # Balance sheet
├── GET    /api/reports/vat-summary                 # VAT summary
├── GET    /api/reports/tax-liability               # Tax liability report
├── GET    /api/reports/gift-card-liability         # Gift card liability
└── GET    /api/reports/daily-summary               # Daily financial summary

Exports
├── POST   /api/exports/datev                       # DATEV export (Germany)
├── POST   /api/exports/csv                         # Generic CSV export
└── POST   /api/exports/sage                        # Sage export
└── POST   /api/exports/xero                        # Xero export
└── POST   /api/exports/quickbooks                  # QuickBooks export
```

### Integration with External Accounting Systems

```
┌─────────────────────────────────────────────────────────────────┐
│                    Accounting Service                            │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐             │
│  │ DATEV       │  │ Xero        │  │ QuickBooks  │  ...        │
│  │ Exporter    │  │ Sync        │  │ Sync        │             │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘             │
└─────────┼────────────────┼───────────────┼──────────────────────┘
          │                │               │
          ▼                ▼               ▼
   ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
   │ DATEV       │  │ Xero Cloud  │  │ QuickBooks  │
   │ Unternehmen │  │ Accounting  │  │ Online      │
   │ Online      │  │             │  │             │
   └─────────────┘  └─────────────┘  └─────────────┘
```

### Kafka Events Consumed

```
OrderCompleted          → Generate sales journal entry
PaymentReceived         → Generate payment journal entry
GiftCardIssued          → Generate liability entry
GiftCardRedeemed        → Generate redemption entry
DeliveryReceived        → Generate inventory/payable entry
InventoryConsumed       → Generate COGS entry
CashDrawerClosed        → Generate cash reconciliation
TransactionSigned       → Link fiscal transaction to entry
```

### Kafka Events Published

```
JournalEntryPosted
├── EntryId, EntryNumber, TotalDebit, TotalCredit, SourceType, SourceId

AccountingPeriodClosed
├── PeriodId, LocationId, StartDate, EndDate, TotalRevenue, TotalExpenses

ReconciliationVarianceDetected
├── ReconciliationId, Type, ExpectedAmount, ActualAmount, Variance
```

---

## 4. Multi-Tenancy Architecture

Transform the existing multi-location design into a true multi-tenant SaaS architecture with proper isolation.

### Tenant Hierarchy

```
Platform (DarkVelocity)
└── Tenant (Restaurant Group/Company)
    ├── Organization Settings
    ├── Subscription/Billing
    ├── Users (with tenant-wide roles)
    └── Locations
        ├── Location Settings
        ├── Local Users (with location roles)
        ├── Hardware
        └── Operational Data
```

### Isolation Strategy: Schema-per-Tenant

For regulatory compliance (especially fiscal data), use PostgreSQL schemas:

```
Database: darkvelocity_prod
├── Schema: public              # Shared platform data (tenants, subscriptions)
├── Schema: tenant_a1b2c3d4     # Tenant A's data
├── Schema: tenant_e5f6g7h8     # Tenant B's data
└── Schema: tenant_...          # More tenants
```

**Advantages:**
- Strong data isolation (required for fiscal compliance)
- Easy tenant backup/restore
- Tenant-specific migrations possible
- Clear audit trail per tenant

### Tenant Entity

```
Tenant
├── Id (Guid)
├── Code (string) - URL-safe identifier
├── Name (string)
├── LegalName (string)
├── TaxId (string)
├── Country (string, ISO 3166)
├── Currency (string, ISO 4217)
├── Timezone (string)
├── Status (Trial, Active, Suspended, Cancelled)
├── SubscriptionTier (Starter, Professional, Enterprise)
├── Features (JSON) - enabled features
├── Settings (JSON) - tenant-wide config
├── SchemaName (string) - PostgreSQL schema
├── CreatedAt (DateTime)
├── TrialEndsAt (DateTime?)
└── BillingEmail (string)

TenantSubscription
├── Id (Guid)
├── TenantId (Guid)
├── PlanId (string)
├── Status (Active, PastDue, Cancelled)
├── CurrentPeriodStart (DateTime)
├── CurrentPeriodEnd (DateTime)
├── Locations (int) - included locations
├── Users (int) - included users
├── StripeSubscriptionId (string?)
└── StripeCustomerId (string?)
```

### Request Context & Routing

Every request includes tenant context derived from:

1. **Subdomain**: `tenant-code.darkvelocity.io`
2. **Header**: `X-Tenant-ID` (for API clients)
3. **JWT Claim**: `tenant_id` (from auth token)

```csharp
public class TenantContext
{
    public Guid TenantId { get; init; }
    public string TenantCode { get; init; }
    public string SchemaName { get; init; }
    public Guid? LocationId { get; init; }  // If location-scoped request
    public TenantFeatures Features { get; init; }
}

// Middleware sets this per-request
public class TenantMiddleware
{
    public async Task InvokeAsync(HttpContext context, ITenantResolver resolver)
    {
        var tenant = await resolver.ResolveAsync(context);
        context.Items["TenantContext"] = tenant;

        // Set EF Core schema for this request
        var dbContext = context.RequestServices.GetRequiredService<AppDbContext>();
        dbContext.SetSchema(tenant.SchemaName);

        await _next(context);
    }
}
```

### Database Context Configuration

```csharp
public class TenantDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;
    private string _schema = "public";

    public void SetSchema(string schema)
    {
        _schema = schema;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // All entities go to tenant schema
        modelBuilder.HasDefaultSchema(_schema);

        // Apply global query filters for tenant isolation
        modelBuilder.Entity<Location>()
            .HasQueryFilter(l => l.TenantId == _tenantContext.TenantId);
        // ... repeat for all tenant-scoped entities
    }
}
```

### Cross-Tenant Operations (Admin Only)

Platform-level operations for system administrators:

```
Platform Admin API (Internal)
├── GET    /admin/tenants                    # List all tenants
├── POST   /admin/tenants                    # Create tenant (provisions schema)
├── GET    /admin/tenants/{id}               # Get tenant details
├── POST   /admin/tenants/{id}/suspend       # Suspend tenant
├── POST   /admin/tenants/{id}/provision     # Re-provision schema
├── DELETE /admin/tenants/{id}               # Delete tenant (archive data first)
├── GET    /admin/tenants/{id}/metrics       # Usage metrics
└── POST   /admin/tenants/{id}/migrate       # Run pending migrations
```

### Tenant Provisioning Workflow

```
1. Tenant Signs Up
   └── Create Tenant record (public schema)

2. Schema Provisioning
   ├── CREATE SCHEMA tenant_{code}
   ├── Run all EF migrations against new schema
   └── Seed default data (accounts, tax rates, etc.)

3. Initial Setup
   ├── Create admin user
   ├── Create first location
   └── Configure fiscal device (if required)

4. Ready for Use
   └── Tenant accesses via subdomain
```

### Multi-Tenant Event Bus

Kafka topics include tenant context:

```
Topic: orders.completed
Message:
{
  "eventId": "...",
  "tenantId": "abc123",
  "locationId": "...",
  "orderId": "...",
  "completedAt": "..."
}
```

Consumers filter by tenant when needed:

```csharp
[Consumer("orders.completed")]
public async Task HandleOrderCompleted(OrderCompletedEvent evt)
{
    using var scope = _scopeFactory.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AccountingDbContext>();
    db.SetSchema($"tenant_{evt.TenantCode}");

    // Process event with correct tenant context
    await GenerateJournalEntry(db, evt);
}
```

---

## 5. Kubernetes-Native Deployment (No API Gateway)

Remove the API Gateway in favor of Kubernetes-native service mesh and ingress.

### Why Remove API Gateway?

1. **Kubernetes provides routing** - Ingress controllers handle path-based routing
2. **Service mesh for cross-cutting** - Istio/Linkerd handle auth, rate limiting, observability
3. **Simpler architecture** - One less component to maintain
4. **Direct service access** - Lower latency, easier debugging

### Architecture Without Gateway

```
┌─────────────────────────────────────────────────────────────────┐
│                         Internet                                 │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Kubernetes Ingress                            │
│                (NGINX Ingress Controller)                        │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │ Rules:                                                       ││
│  │  /api/auth/*       → auth-service:5000                      ││
│  │  /api/menu/*       → menu-service:5002                      ││
│  │  /api/orders/*     → orders-service:5003                    ││
│  │  /api/payments/*   → payments-service:5004                  ││
│  │  /api/giftcards/*  → giftcards-service:5012                 ││
│  │  /api/fiscal/*     → fiscalisation-service:5013             ││
│  │  /api/accounting/* → accounting-service:5014                ││
│  │  /*                → frontend-service                        ││
│  └─────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│               Kubernetes Service Mesh (Istio)                    │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │ Sidecar Proxies (Envoy) for each pod:                     │  │
│  │  • mTLS between services                                  │  │
│  │  • JWT validation (tenant context)                        │  │
│  │  • Rate limiting per tenant                               │  │
│  │  • Circuit breaking                                       │  │
│  │  • Distributed tracing                                    │  │
│  │  • Metrics collection                                     │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                              │
          ┌───────────────────┼───────────────────┐
          ▼                   ▼                   ▼
┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│  auth-service   │  │  menu-service   │  │ orders-service  │
│     (Pod)       │  │     (Pod)       │  │     (Pod)       │
│  ┌───────────┐  │  │  ┌───────────┐  │  │  ┌───────────┐  │
│  │  Envoy    │  │  │  │  Envoy    │  │  │  │  Envoy    │  │
│  │  Sidecar  │  │  │  │  Sidecar  │  │  │  │  Sidecar  │  │
│  └───────────┘  │  │  └───────────┘  │  │  └───────────┘  │
│  ┌───────────┐  │  │  ┌───────────┐  │  │  ┌───────────┐  │
│  │  .NET App │  │  │  │  .NET App │  │  │  │  .NET App │  │
│  └───────────┘  │  │  └───────────┘  │  │  └───────────┘  │
└─────────────────┘  └─────────────────┘  └─────────────────┘
```

### Kubernetes Manifests Structure

```
k8s/
├── base/
│   ├── namespace.yaml
│   ├── configmap.yaml              # Shared config
│   ├── secrets.yaml                # (reference to external secrets)
│   └── services/
│       ├── auth/
│       │   ├── deployment.yaml
│       │   ├── service.yaml
│       │   └── hpa.yaml            # Horizontal Pod Autoscaler
│       ├── menu/
│       ├── orders/
│       ├── payments/
│       ├── giftcards/
│       ├── fiscalisation/
│       ├── accounting/
│       └── ...
├── overlays/
│   ├── development/
│   │   ├── kustomization.yaml
│   │   └── patches/
│   ├── staging/
│   │   ├── kustomization.yaml
│   │   └── patches/
│   └── production/
│       ├── kustomization.yaml
│       ├── patches/
│       └── ingress.yaml
└── istio/
    ├── gateway.yaml
    ├── virtual-services.yaml
    ├── destination-rules.yaml
    ├── authorization-policies.yaml
    └── peer-authentication.yaml
```

### Sample Service Deployment

```yaml
# k8s/base/services/accounting/deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: accounting-service
  labels:
    app: accounting
    version: v1
spec:
  replicas: 2
  selector:
    matchLabels:
      app: accounting
  template:
    metadata:
      labels:
        app: accounting
        version: v1
      annotations:
        sidecar.istio.io/inject: "true"
    spec:
      containers:
      - name: accounting
        image: darkvelocity/accounting-service:latest
        ports:
        - containerPort: 5014
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: db-credentials
              key: connection-string
        resources:
          requests:
            memory: "256Mi"
            cpu: "100m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 5014
          initialDelaySeconds: 10
          periodSeconds: 5
        livenessProbe:
          httpGet:
            path: /health/live
            port: 5014
          initialDelaySeconds: 15
          periodSeconds: 10
```

### Ingress Configuration

```yaml
# k8s/overlays/production/ingress.yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: darkvelocity-ingress
  annotations:
    kubernetes.io/ingress.class: nginx
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
    nginx.ingress.kubernetes.io/proxy-body-size: "10m"
    cert-manager.io/cluster-issuer: letsencrypt-prod
spec:
  tls:
  - hosts:
    - "*.darkvelocity.io"
    secretName: darkvelocity-tls
  rules:
  - host: "*.darkvelocity.io"
    http:
      paths:
      - path: /api/auth
        pathType: Prefix
        backend:
          service:
            name: auth-service
            port:
              number: 5000
      - path: /api/locations
        pathType: Prefix
        backend:
          service:
            name: location-service
            port:
              number: 5001
      - path: /api/menu
        pathType: Prefix
        backend:
          service:
            name: menu-service
            port:
              number: 5002
      - path: /api/orders
        pathType: Prefix
        backend:
          service:
            name: orders-service
            port:
              number: 5003
      - path: /api/payments
        pathType: Prefix
        backend:
          service:
            name: payments-service
            port:
              number: 5004
      - path: /api/hardware
        pathType: Prefix
        backend:
          service:
            name: hardware-service
            port:
              number: 5005
      - path: /api/inventory
        pathType: Prefix
        backend:
          service:
            name: inventory-service
            port:
              number: 5006
      - path: /api/procurement
        pathType: Prefix
        backend:
          service:
            name: procurement-service
            port:
              number: 5007
      - path: /api/costing
        pathType: Prefix
        backend:
          service:
            name: costing-service
            port:
              number: 5008
      - path: /api/reports
        pathType: Prefix
        backend:
          service:
            name: reporting-service
            port:
              number: 5009
      - path: /api/bookings
        pathType: Prefix
        backend:
          service:
            name: booking-service
            port:
              number: 5010
      - path: /api/payment-gateway
        pathType: Prefix
        backend:
          service:
            name: payment-gateway-service
            port:
              number: 5011
      - path: /api/giftcards
        pathType: Prefix
        backend:
          service:
            name: giftcards-service
            port:
              number: 5012
      - path: /api/fiscal
        pathType: Prefix
        backend:
          service:
            name: fiscalisation-service
            port:
              number: 5013
      - path: /api/accounting
        pathType: Prefix
        backend:
          service:
            name: accounting-service
            port:
              number: 5014
      - path: /
        pathType: Prefix
        backend:
          service:
            name: frontend-service
            port:
              number: 80
```

### Istio Authorization (JWT Validation)

```yaml
# k8s/istio/authorization-policies.yaml
apiVersion: security.istio.io/v1beta1
kind: AuthorizationPolicy
metadata:
  name: require-jwt
  namespace: darkvelocity
spec:
  selector:
    matchLabels:
      app.kubernetes.io/part-of: darkvelocity
  action: ALLOW
  rules:
  - from:
    - source:
        requestPrincipals: ["*"]
    when:
    - key: request.auth.claims[tenant_id]
      values: ["*"]
---
apiVersion: security.istio.io/v1beta1
kind: RequestAuthentication
metadata:
  name: jwt-auth
  namespace: darkvelocity
spec:
  selector:
    matchLabels:
      app.kubernetes.io/part-of: darkvelocity
  jwtRules:
  - issuer: "https://auth.darkvelocity.io"
    jwksUri: "https://auth.darkvelocity.io/.well-known/jwks.json"
    forwardOriginalToken: true
```

### Rate Limiting per Tenant

```yaml
# k8s/istio/envoy-filter-ratelimit.yaml
apiVersion: networking.istio.io/v1alpha3
kind: EnvoyFilter
metadata:
  name: tenant-rate-limit
  namespace: darkvelocity
spec:
  workloadSelector:
    labels:
      app.kubernetes.io/part-of: darkvelocity
  configPatches:
  - applyTo: HTTP_FILTER
    match:
      context: SIDECAR_INBOUND
    patch:
      operation: INSERT_BEFORE
      value:
        name: envoy.filters.http.local_ratelimit
        typed_config:
          "@type": type.googleapis.com/udpa.type.v1.TypedStruct
          type_url: type.googleapis.com/envoy.extensions.filters.http.local_ratelimit.v3.LocalRateLimit
          value:
            stat_prefix: http_local_rate_limiter
            token_bucket:
              max_tokens: 1000
              tokens_per_fill: 100
              fill_interval: 1s
            filter_enabled:
              runtime_key: local_rate_limit_enabled
              default_value:
                numerator: 100
                denominator: HUNDRED
```

### Removing API Gateway Project

1. Delete `src/Gateway/` directory
2. Remove from solution file
3. Update README to remove gateway references
4. Update docker-compose to remove gateway service
5. Update service-to-service calls to use Kubernetes DNS:
   - Old: `http://gateway:8000/api/menu/...`
   - New: `http://menu-service.darkvelocity.svc.cluster.local:5002/api/...`

---

## 6. Implementation Phases

### Phase 1: Foundation (Weeks 1-2)

**Multi-Tenancy Core**
- [ ] Design Tenant entity and provisioning
- [ ] Implement schema-per-tenant in EF Core
- [ ] Create TenantMiddleware and context resolution
- [ ] Add tenant filters to all existing entities
- [ ] Update all services with tenant awareness

**Kubernetes Setup**
- [ ] Create base Kubernetes manifests
- [ ] Set up Ingress controller
- [ ] Configure Istio service mesh
- [ ] Implement health check endpoints
- [ ] Create Helm charts or Kustomize overlays
- [ ] Remove API Gateway project

### Phase 2: Gift Cards (Weeks 3-4)

- [ ] Create GiftCards.Api service
- [ ] Implement card lifecycle (issue, activate, redeem)
- [ ] Add gift card programs and designs
- [ ] Integrate with Payments service
- [ ] Add gift card as payment method in Orders
- [ ] Implement balance and liability reporting
- [ ] Write integration tests

### Phase 3: Fiscalisation (Weeks 5-7)

- [ ] Create Fiscalisation.Api service
- [ ] Implement TSE adapter interface
- [ ] Integrate Fiskaly Cloud TSE (primary)
- [ ] Implement transaction signing flow
- [ ] Add QR code generation for receipts
- [ ] Implement DSFinV-K export
- [ ] Create fiscal journal and audit log
- [ ] Write integration tests with mock TSE

### Phase 4: Accounting (Weeks 8-10)

- [ ] Create Accounting.Api service
- [ ] Implement Chart of Accounts
- [ ] Create journal entry automation
- [ ] Add accounting period management
- [ ] Implement reconciliation workflows
- [ ] Create financial reports (P&L, Balance Sheet)
- [ ] Add DATEV export (Germany)
- [ ] Write integration tests

### Phase 5: Integration & Polish (Weeks 11-12)

- [ ] End-to-end testing across all services
- [ ] Performance testing and optimization
- [ ] Security audit and penetration testing
- [ ] Documentation update
- [ ] Production deployment procedures
- [ ] Monitoring and alerting setup

---

## Service Port Summary (Updated)

| Service | Port | Description |
|---------|------|-------------|
| Auth | 5000 | Authentication, users, permissions |
| Location | 5001 | Multi-location management |
| Menu | 5002 | Items, categories, menus |
| Orders | 5003 | Order lifecycle, sales periods |
| Payments | 5004 | Payment processing |
| Hardware | 5005 | POS devices, printers, drawers |
| Inventory | 5006 | Stock tracking, FIFO |
| Procurement | 5007 | Suppliers, POs, deliveries |
| Costing | 5008 | Recipes, cost calculations |
| Reporting | 5009 | Sales and margin reports |
| Booking | 5010 | Table reservations |
| PaymentGateway | 5011 | External payment processors |
| **GiftCards** | **5012** | **Gift card management** |
| **Fiscalisation** | **5013** | **TSE/KassenSichV compliance** |
| **Accounting** | **5014** | **Financial records, journals** |

---

## Dependencies Diagram

```
                           ┌──────────────┐
                           │    Auth      │
                           │   Service    │
                           └──────┬───────┘
                                  │ (JWT tokens)
                                  ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                          All Services                                     │
└──────────────────────────────────────────────────────────────────────────┘
                                  │
          ┌───────────────────────┼───────────────────────┐
          ▼                       ▼                       ▼
┌─────────────────┐      ┌─────────────────┐      ┌─────────────────┐
│    Orders       │      │   GiftCards     │      │   Inventory     │
│    Service      │      │   Service       │      │   Service       │
└────────┬────────┘      └────────┬────────┘      └────────┬────────┘
         │                        │                        │
         │   ┌────────────────────┼────────────────────────┘
         │   │                    │
         ▼   ▼                    ▼
┌─────────────────┐      ┌─────────────────┐
│   Payments      │      │ Fiscalisation   │
│   Service       │──────│   Service       │
└────────┬────────┘      └────────┬────────┘
         │                        │
         │                        │
         ▼                        ▼
┌─────────────────────────────────────────┐
│           Accounting Service             │
│  (Central financial record keeping)      │
└─────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────┐
│           External Systems               │
│  (DATEV, Xero, QuickBooks, etc.)         │
└─────────────────────────────────────────┘
```

---

*Document Version: 1.0*
*Last Updated: January 2026*
*Author: DarkVelocity Engineering*
