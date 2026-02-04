# DarkVelocity POS - Product Completeness Analysis

> **Analysis Date:** February 2026
> **Analyzed By:** Claude Code Domain Analysis Agents
> **Purpose:** Identify gaps and improvements needed for production readiness

## Executive Summary

This document provides a comprehensive analysis of all 17 grain domains in the DarkVelocity POS system, evaluating product completeness and identifying specific actionable improvements needed for a production-ready hospitality platform.

### Overall Assessment

| Category | Count | Status |
|----------|-------|--------|
| Domains Analyzed | 17 | Complete |
| Production Ready | 2 | Menu, Payments (core) |
| Near Complete (70%+) | 5 | Orders, Customers, Organization, Payments, Menu |
| Partial (50-70%) | 6 | Inventory, Tables, Staff, Reporting, Recipes, Costing |
| Significant Gaps (<50%) | 6 | Payment Processors, Fiscal, System, Finance, External Channels, Devices |

### Critical Blockers (P0)

1. **Devices Domain** - No offline mode or printing system
2. **External Channels Domain** - No webhook handlers or HTTP clients for delivery platforms
3. **Payment Processors Domain** - No actual Stripe/Adyen implementation
4. **Fiscal Domain** - No API endpoints or TSE adapters for EU compliance
5. **System Domain** - Notifications don't send, webhooks don't deliver

---

## Domain Analysis Details

### 1. Orders Domain

**Completeness: 70%**

#### Current Implementation
- Full order lifecycle with event sourcing
- Order types: DineIn, TakeOut, Delivery, DriveThru, Online, Tab
- Per-item tax rates for multiple jurisdictions
- Order-level discounts with approval tracking
- Bill splitting by items
- Table and server transfers
- Kitchen ticket integration via Orleans streams

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| Course Management | High | 1-2 weeks | No course-based firing, holding, or timing coordination |
| Seat Assignment | High | 1 week | Cannot assign items to seats for seat-based splitting |
| Order Merging | Medium | 3-5 days | Can split but cannot merge orders (combining tables) |
| Line-Level Discounts | High | 1 week | Only order-level discounts exist |
| Hold/Rush/Recall | High | 1 week | Cannot hold orders before kitchen, rush, or recall |
| Order History API | Medium | 3-5 days | Event sourcing exists but no timeline view endpoint |

#### Recommended Actions

```
Phase 1 (Essential):
├── Implement SetLineCourseAsync() and FireCourseAsync()
├── Add Seat property to OrderLine
├── Add line-level discount methods
└── Implement HoldOrderAsync() and RushOrderAsync()

Phase 2 (Enhanced):
├── Implement MergeFromOrderAsync()
├── Create OrderHistoryGrain for audit timeline
├── Add order search and filtering endpoints
└── Coordinate kitchen ticket voids
```

#### Key Files
- `/src/DarkVelocity.Host/Domains/Orders/OrderGrain.cs`
- `/src/DarkVelocity.Host/Domains/Orders/OrderState.cs`
- `/src/DarkVelocity.Host/Domains/Orders/KitchenGrain.cs`

---

### 2. Payments Domain

**Completeness: 75%**

#### Current Implementation
- Full payment lifecycle with event sourcing
- Split and partial payments
- Card authorization flow
- Cash drawer management with variance tracking
- Gift card lifecycle (activation, redemption, reload)
- Refund support (full and partial)
- Payment batch tracking structure

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| Settlement Batch | Critical | 1-2 weeks | Interface exists but no implementation |
| House Accounts | High | 2 weeks | Customer AR for tabs and corporate accounts |
| Loyalty Integration | High | 1 week | PaymentMethod.LoyaltyPoints exists but no processing |
| Offline Queue | Critical | 1-2 weeks | No store-and-forward for network failures |
| Chargeback Handling | Medium | 1 week | No dispute workflow |
| Surcharges | Medium | 3-5 days | No card processing fee handling |

#### Recommended Actions

```
Phase 1 (Critical):
├── Implement ISettlementBatchGrain
├── Create OfflinePaymentQueueGrain
├── Add payment failure retry logic
└── Implement chargeback workflow

Phase 2 (High Value):
├── Create CustomerAccountGrain for house accounts
├── Integrate LoyaltyProgramGrain with payments
├── Add surcharge calculation
└── Implement tip pooling integration
```

#### Key Files
- `/src/DarkVelocity.Host/Domains/Payments/PaymentGrain.cs`
- `/src/DarkVelocity.Host/Domains/Payments/GiftCardGrain.cs`
- `/src/DarkVelocity.Host/Domains/Shared/IBatchGrains.cs`

---

### 3. Payment Processors Domain

**Completeness: 30%**

#### Current Implementation
- Merchant and Terminal grain interfaces
- MockProcessorGrain for testing
- Payment Intent flow (Stripe-compatible)
- Webhook endpoint structure

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| Stripe Implementation | Critical | 2-3 weeks | Interface only, no actual SDK integration |
| Adyen Implementation | Critical | 2-3 weeks | Interface only, no actual SDK integration |
| Offline Payments | Critical | 1-2 weeks | No store-forward queue |
| Retry Logic | High | 1 week | State tracks retries but no implementation |
| Terminal Pairing | High | 1 week | Basic registration only |
| PCI Compliance | High | 2 weeks | No tokenization enforcement or audit logging |

#### Recommended Actions

```
Phase 1 (Critical):
├── Implement StripeProcessorGrain with SDK
├── Implement AdyenProcessorGrain with SDK
├── Create OfflinePaymentQueueGrain
├── Add idempotency key management
└── Implement retry with exponential backoff

Phase 2 (Production):
├── Add terminal pairing workflow
├── Implement settlement reporting
├── Create DisputeGrain for chargebacks
├── Add processor routing/failover
└── Implement PCI audit logging
```

#### Key Files to Create
- `/src/DarkVelocity.Host/Domains/Payments/StripeProcessorGrain.cs`
- `/src/DarkVelocity.Host/Domains/Payments/AdyenProcessorGrain.cs`
- `/src/DarkVelocity.Host/Domains/Payments/OfflinePaymentQueueGrain.cs`
- `/src/DarkVelocity.Host/Domains/Payments/DisputeGrain.cs`

---

### 4. Customers Domain

**Completeness: 65%**

#### Current Implementation
- Customer profile management with event sourcing
- Loyalty enrollment and points
- Rewards issuance and redemption
- Visit history tracking (last 50)
- Preferences and dietary restrictions
- Referral code generation
- GDPR compliance (anonymize/delete)

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| Birthday Rewards | High | 3-5 days | Defined but never triggered |
| Segmentation | High | 1 week | Set to "New" on creation, never updated |
| Marketing Consent | High | 3-5 days | Flags exist but no capture workflow (GDPR) |
| Points Expiration | High | 3-5 days | Config exists but never enforced |
| Redemption Limits | Medium | 2-3 days | Config exists but not checked |
| Visit History Grain | Medium | 3-5 days | Interface defined, implementation partial |

#### Recommended Actions

```
Phase 1 (Essential):
├── Create birthday reward background job
├── Implement RFM segmentation automation
├── Add marketing consent capture with audit trail
├── Create points expiration job
└── Enforce redemption limits in RedeemRewardAsync()

Phase 2 (Enhanced):
├── Complete CustomerVisitHistoryGrain
├── Add customer feedback/review system
├── Implement multi-channel identity resolution
├── Add customer journey tracking
└── Build tier maintenance job
```

#### Key Files
- `/src/DarkVelocity.Host/Domains/Customers/CustomerGrain.cs`
- `/src/DarkVelocity.Host/Domains/Customers/LoyaltyProgramGrain.cs`
- `/src/DarkVelocity.Host/Domains/Customers/CustomerSpendProjectionGrain.cs`

---

### 5. Inventory Domain

**Completeness: 60%**

#### Current Implementation
- FIFO batch/lot tracking with expiry
- Weighted average cost calculation
- Stock movements (receipt, consumption, waste, transfer, adjustment)
- Reorder points and par levels
- Negative stock support (matches philosophy)
- Supplier and purchase order management
- AI-powered invoice/receipt processing

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| Stock Take Workflow | High | 2 weeks | Basic count only, no scheduling or approval |
| Transfer Grain | Medium | 1-2 weeks | Basic in/out, no lifecycle management |
| Variance Reporting | High | 1 week | Tracks but no analysis or alerts |
| Stock Optimization | Medium | 1-2 weeks | No ABC analysis or dynamic reorder points |
| Expiry Monitoring | High | 3-5 days | No background job for expiry alerts |
| Procurement API | High | 1 week | Grains exist but limited endpoint exposure |

#### Recommended Actions

```
Phase 1 (Essential):
├── Create StockTakeGrain with full workflow
├── Add variance analysis and alerting
├── Implement expiry monitoring background job
├── Complete procurement API endpoints
└── Create transfer lifecycle grain

Phase 2 (Advanced):
├── Implement ABC classification
├── Add automatic reorder suggestions
├── Create multi-location inventory view
├── Add reservation/allocation system
└── Implement supplier performance tracking
```

#### Key Files
- `/src/DarkVelocity.Host/Domains/Inventory/InventoryGrain.cs`
- `/src/DarkVelocity.Host/Domains/Procurement/ProcurementGrains.cs`
- `/src/DarkVelocity.Host/Domains/Procurement/PurchaseDocumentGrain.cs`

---

### 6. Menu Domain

**Completeness: 80%**

#### Current Implementation
- Event-sourced CMS with draft/publish workflow
- Multi-language localization
- Nutrition and allergen data
- 86'd items with snooze duration
- Daypart availability windows
- Site-specific overrides
- Channel visibility (POS, online, delivery)
- Reusable modifier blocks

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| Combo/Bundle Items | High | 1-2 weeks | No meal deal or bundle support |
| Dynamic Pricing | High | 1 week | Availability exists but no price changes |
| Upsell Prompts | Medium | 3-5 days | No cross-sell or upsell suggestions |
| Auto-86 from Inventory | High | 3-5 days | Manual snooze only |
| Price Tiers | Medium | 1 week | No VIP/staff/loyalty pricing |
| Menu Engineering | Medium | 3-5 days | Grain exists but not integrated |

#### Recommended Actions

```
Phase 1 (Essential):
├── Add ComboComponent to MenuItemVersionState
├── Implement dynamic pricing in AvailabilityWindow
├── Integrate inventory for auto-snoozing
└── Connect menu engineering data to resolution

Phase 2 (Enhanced):
├── Add UpsellPrompt support
├── Implement PriceTier for customer segments
├── Add auto-unsnooze background job
├── Enhance media support (gallery, video)
└── Add modifier availability windows
```

#### Key Files
- `/src/DarkVelocity.Host/Domains/Menu/MenuCmsState.cs`
- `/src/DarkVelocity.Host/Domains/Menu/MenuResolverGrains.cs`
- `/src/DarkVelocity.Host/Domains/Menu/MenuEngineeringGrain.cs`

---

### 7. Recipes Domain

**Completeness: 55%**

#### Current Implementation
- Recipe CMS with version control
- Localization support
- Ingredient management with waste percentage
- Batch prep configuration
- Menu item linkage
- Separate costing grains with price history

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| Domain Fragmentation | High | 1-2 weeks | RecipeCMS and Costing domains disconnected |
| Sub-Recipes | High | 1-2 weeks | Cannot use recipes as ingredients |
| Recipe Scaling | Medium | 1 week | Cannot adjust for different batch sizes |
| Allergen Inheritance | High | 1 week | Manual tagging, not computed from ingredients |
| Nutritional Calculation | Medium | 1 week | Not calculated from ingredients |
| Production Tracking | Medium | 1-2 weeks | Events defined but no grain |

#### Recommended Actions

```
Phase 1 (Critical):
├── Consolidate recipe costing into RecipeDocumentGrain
├── Create IIngredientGrain for definitions
├── Implement allergen inheritance from ingredients
└── Add recipe validation rules

Phase 2 (Enhanced):
├── Add sub-recipe/component support
├── Implement recipe scaling
├── Add nutritional calculation
├── Create batch production tracking grain
└── Build menu engineering integration
```

#### Key Files
- `/src/DarkVelocity.Host/Domains/Menu/RecipeCmsGrains.cs`
- `/src/DarkVelocity.Host/Domains/Costing/CostingGrains.cs`

---

### 8. Costing Domain

**Completeness: 50%**

#### Current Implementation
- Recipe cost calculation
- Ingredient price tracking with history
- Cost alerts with acknowledgment
- Costing settings with thresholds
- Menu engineering (BCG matrix)
- FIFO/LIFO/WAC costing policies

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| NO API ENDPOINTS | Critical | 1-2 days | Zero REST API exposure |
| Alert Index | High | 3-5 days | Cannot query alerts by type/status |
| Menu Engineering Integration | Medium | 1 week | Disconnected from real sales data |
| Profitability Dashboard | High | 1 week | No aggregated profitability view |
| Variance Analysis | Medium | 1 week | No theoretical vs actual reconciliation |
| Cost Trending | Low | 1 week | No forecasting or trending |

#### Recommended Actions

```
Phase 1 (Critical):
├── Create CostingEndpoints.cs (all grains)
├── Create CostAlertIndexGrain
├── Add Orleans stream subscriber for auto-updates
└── Implement profitability dashboard grain

Phase 2 (Enhanced):
├── Enhance price recommendations with elasticity
├── Create cost variance analysis grain
├── Add cost trending and forecasting
├── Implement scenario modeling
└── Enhance accounting groups for P&L
```

#### Key Files
- `/src/DarkVelocity.Host/Domains/Costing/CostingGrains.cs`
- `/src/DarkVelocity.Host/Domains/Menu/MenuEngineeringGrain.cs`

#### Files to Create
- `/src/DarkVelocity.Host/Domains/Costing/CostingEndpoints.cs`
- `/src/DarkVelocity.Host/Domains/Costing/CostAlertIndexGrain.cs`

---

### 9. Tables & Bookings Domain

**Completeness: 60%**

#### Current Implementation
- Booking lifecycle with event sourcing
- Table status management
- Floor plan support
- Waitlist with quoted times
- Booking settings per site
- Booking calendar aggregation
- Deposit lifecycle

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| Calendar Auto-Sync | Critical | 3-5 days | Manual sync, data drifts |
| Notifications | High | 1-2 weeks | No confirmation or reminder sending |
| No-Show Detection | High | 3-5 days | Manual marking only |
| Deposit Payment Links | High | 1 week | Cannot generate online payment links |
| Table Assignment | Medium | 1-2 weeks | No optimization or suggestions |
| Turn Time Tracking | Medium | 1 week | No actual turn time calculation |

#### Recommended Actions

```
Phase 1 (Critical):
├── Create booking stream subscriber for calendar sync
├── Implement NotificationSchedulerGrain
├── Add no-show auto-detection via Orleans reminders
├── Integrate Stripe for deposit payment links
└── Complete CustomerVisitHistoryGrain

Phase 2 (Enhanced):
├── Create TableAssignmentOptimizerGrain
├── Build TableAnalyticsGrain for turn times
├── Add channel-specific booking rules
├── Enhance occasion handling
└── Implement waitlist SMS notifications
```

#### Key Files
- `/src/DarkVelocity.Host/Domains/Bookings/BookingGrain.cs`
- `/src/DarkVelocity.Host/Domains/Bookings/TableGrain.cs`

---

### 10. Staff Domain

**Completeness: 55%**

#### Current Implementation
- Employee lifecycle management
- Clock in/out with method tracking
- Role and permission management
- Schedule management with labor cost
- Time entry tracking
- Tip pool distribution
- Payroll period aggregation
- Availability and time off requests
- Shift swap workflow

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| Break Tracking | High | 3-5 days | Events defined but not implemented |
| Overtime Calculation | High | 1 week | Only >8 hours/day, no weekly |
| Certifications | Medium | 1 week | Role requires but not tracked |
| Performance Metrics | Medium | 1-2 weeks | No tracking at all |
| Payroll Export | High | 2 weeks | Stub only, no actual export |
| Labor Compliance | High | 2 weeks | No validation of labor laws |
| NO TESTS | Critical | 2 weeks | Zero test coverage |

#### Recommended Actions

```
Phase 1 (Critical):
├── Implement break start/end in EmployeeGrain
├── Add weekly overtime calculation
├── Create payroll export service (ADP, Gusto, CSV)
├── Add labor compliance validation
└── Write comprehensive test suite

Phase 2 (Enhanced):
├── Create certification tracking grain
├── Implement performance metrics
├── Add shift templates
├── Create labor cost forecasting
└── Add schedule publishing notifications
```

#### Key Files
- `/src/DarkVelocity.Host/Domains/Staff/EmployeeGrain.cs`
- `/src/DarkVelocity.Host/Domains/Staff/LaborGrains.cs`

---

### 11. Finance Domain

**Completeness: 40%**

#### Current Implementation
- Account grain with double-entry
- Journal entries per account
- Expense grain with approval workflow
- Expense index for queries
- Ledger grain (utility for balances)
- Tax rate management

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| Chart of Accounts | Critical | 1-2 weeks | No centralized account registry |
| Journal Entry Grain | Critical | 1-2 weeks | No proper multi-line double-entry |
| Accounting Periods | High | 1 week | Minimal period management |
| Multi-Currency | Medium | 2 weeks | Fields only, no conversion |
| Tax Reporting | High | 1-2 weeks | Basic rates only |
| Expense-to-GL | High | 1 week | Expenses don't post to GL |
| Financial Reports | Critical | 2-3 weeks | No P&L, balance sheet, cash flow |
| External Integration | High | 3-4 weeks | No QuickBooks/Xero sync |

#### Recommended Actions

```
Phase 1 (Critical):
├── Create ChartOfAccountsGrain
├── Create JournalEntryGrain (balanced double-entry)
├── Create AccountingPeriodGrain
├── Add expense-to-GL subscriber
└── Create basic financial reports

Phase 2 (Integration):
├── Implement QuickBooks Online integration
├── Implement Xero integration
├── Add bank reconciliation
├── Create AP/AR grains
└── Add multi-currency support
```

#### Key Files
- `/src/DarkVelocity.Host/Domains/Finance/AccountGrain.cs`
- `/src/DarkVelocity.Host/Domains/Finance/ExpenseGrain.cs`

#### Files to Create
- `/src/DarkVelocity.Host/Domains/Finance/ChartOfAccountsGrain.cs`
- `/src/DarkVelocity.Host/Domains/Finance/JournalEntryGrain.cs`
- `/src/DarkVelocity.Host/Domains/Finance/AccountingPeriodGrain.cs`

---

### 12. External Channels Domain

**Completeness: 40%**

#### Current Implementation
- Channel grain with connection management
- Channel registry per organization
- Status mapping between platforms
- Delivery platform credentials
- External order grain with full data model
- Menu sync tracking
- Platform payout tracking

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| Webhook Handlers | Critical | 2-3 weeks | No inbound webhook endpoints |
| HTTP Clients | Critical | 2-3 weeks | No outbound API calls to platforms |
| OAuth2 M2M | Critical | 1 week | No client credentials grant |
| Platform Adapters | Critical | 3-4 weeks | No Deliverect/UberEats/DoorDash adapters |
| External Order API | High | 3-5 days | Grain exists but no endpoints |
| Menu Push | High | 2 weeks | No menu sync to platforms |
| Payout Reconciliation | Medium | 1-2 weeks | No matching against orders |
| Commission Tracking | Medium | 1 week | No per-order commission |

#### Recommended Actions

```
Phase 1 (Critical):
├── Create Deliverect webhook handler
├── Create Deliverect API client
├── Create DeliverectAdapter for data mapping
├── Add ExternalOrderEndpoints
├── Implement OAuth2 client credentials
└── Add event stream for status notifications

Phase 2 (Additional Platforms):
├── Implement UberEats integration
├── Implement DoorDash integration
├── Create menu sync orchestrator
└── Build payout reconciliation workflow

Phase 3 (Advanced):
├── Add auto-accept engine
├── Implement commission tracking
├── Add driver tracking integration
└── Build analytics dashboard
```

#### Key Files
- `/src/DarkVelocity.Host/Domains/Channels/ChannelGrain.cs`
- `/src/DarkVelocity.Host/Domains/Orders/OrdersGatewayGrains.cs`

#### Files to Create
- `/src/DarkVelocity.Host/Webhooks/DeliverectWebhookHandler.cs`
- `/src/DarkVelocity.Host/Adapters/DeliverectAdapter.cs`
- `/src/DarkVelocity.Host/Clients/DeliverectApiClient.cs`

---

### 13. Devices Domain

**Completeness: 45%**

#### Current Implementation
- Device authorization (OAuth 2.0 device flow)
- Device registration and lifecycle
- Session management with JWT
- POS device management
- Printer configuration
- Cash drawer hardware
- Device status aggregation
- Kitchen display system grains

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| Offline Mode | Critical | 3-4 weeks | No local storage or sync queue |
| Printing System | Critical | 4-5 weeks | No ESC/POS, no print jobs |
| Terminal Pairing | High | 2-3 weeks | Minimal implementation |
| Device Provisioning | High | 1-2 weeks | No setup wizard |
| Device Monitoring | Medium | 2 weeks | Basic heartbeat only |
| Multi-Device Sync | Medium | 3 weeks | No order hand-off or shared state |

#### Recommended Actions

```
Phase 1 (Critical):
├── Implement IndexedDB schema for offline
├── Create sync queue grain
├── Add service worker with offline fallback
├── Create print job management grain
├── Implement ESC/POS command builder
└── Create local print server (Electron/Tauri)

Phase 2 (Production):
├── Build device provisioning wizard
├── Implement Stripe Terminal SDK
├── Add device health telemetry
├── Create multi-device sync via SignalR
└── Add fiscal device integration
```

#### Key Files
- `/src/DarkVelocity.Host/Domains/Devices/DeviceGrain.cs`
- `/src/DarkVelocity.Host/Domains/Devices/DeviceAuthGrain.cs`
- `/apps/pos/` - PWA application

---

### 14. Reporting Domain

**Completeness: 60%**

#### Current Implementation
- Daily sales aggregation
- Daily inventory snapshots
- Daily consumption tracking
- Daily waste tracking
- Period aggregation (weekly/monthly)
- Site dashboard composition
- Comprehensive API endpoints

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| Time-Based Analysis | High | 1-2 weeks | No hourly or day-part breakdown |
| Labor Reports | Critical | 2 weeks | Not implemented at all |
| Payment Reconciliation | High | 1-2 weeks | No tender breakdown |
| Product Mix | Medium | 1 week | Partial only |
| Comp/Void Detail | Medium | 1 week | Total only, no breakdown |
| Tax by Jurisdiction | Medium | 1 week | Aggregate only |
| Data Export | High | 1 week | No CSV/Excel |
| Dashboard Metrics | Medium | 1 week | WTD/PTD are placeholders |

#### Recommended Actions

```
Phase 1 (Critical):
├── Add SalesByHour to DailySalesState
├── Create labor reporting grains
├── Add payment method breakdown
├── Implement data export endpoints
└── Complete dashboard calculations

Phase 2 (Enhanced):
├── Add product performance grain
├── Implement comp/void breakdown
├── Add tax by rate reporting
├── Create variance trending
└── Build comparative reports (YoY)
```

#### Key Files
- `/src/DarkVelocity.Host/Domains/Reporting/ReportingGrains.cs`
- `/src/DarkVelocity.Host/Domains/Reporting/ReportingEndpoints.cs`

---

### 15. Fiscal Domain

**Completeness: 35%**

#### Current Implementation
- Fiscal device grain with counters
- Fiscal transaction lifecycle
- Fiscal journal (append-only)
- Tax rate management
- Multi-device type support (Swissbit, Fiskaly, Epson, Diebold)
- Comprehensive test coverage

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| NO API ENDPOINTS | Critical | 2-3 days | Zero REST API |
| NO TSE ADAPTERS | Critical | 2-3 weeks | Interfaces only |
| NO DSFinV-K EXPORT | Critical | 1-2 weeks | Compliance requirement |
| Germany Only | High | 4-6 weeks | No Austria, France, Italy, Poland |
| No Order Integration | Critical | 2-3 days | Operates in isolation |
| No Device Lifecycle | High | 1-2 weeks | Basic registration only |
| No Background Jobs | High | 1 week | No scheduled tasks |

#### Recommended Actions

```
Phase 1 (MVP - Germany):
├── Create FiscalEndpoints.cs
├── Implement MockTseAdapter
├── Integrate with OrderGrain
├── Create basic DSFinV-K export
└── Implement FiskalyCloudAdapter

Phase 2 (Production):
├── Add device lifecycle management
├── Create background jobs (Z-report, cert expiry)
├── Complete DSFinV-K export tables
└── Add receipt numbering

Phase 3 (Multi-Country):
├── Implement Austria (RKSV)
├── Implement France (NF 525)
├── Implement Italy (RT)
└── Implement Poland (JPK/KSeF)
```

#### Key Files
- `/src/DarkVelocity.Host/Domains/Fiscal/FiscalisationGrains.cs`

#### Files to Create
- `/src/DarkVelocity.Host/Domains/Fiscal/FiscalEndpoints.cs`
- `/src/DarkVelocity.Host/Domains/Fiscal/Adapters/FiskalyCloudAdapter.cs`
- `/src/DarkVelocity.Host/Domains/Fiscal/Export/DSFinVKExporter.cs`

---

### 16. Organization Domain

**Completeness: 70%**

#### Current Implementation
- Organization CRUD with status management
- Site management with timezone/currency
- User authentication (PIN, OAuth)
- User groups
- SpiceDB authorization schema
- Email and OAuth lookup grains

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| No Event Sourcing | High | 1-2 weeks | Simple state, no audit trail |
| No Branding | High | 1 week | No logo, colors, theme |
| No Plan Management | High | 1-2 weeks | No tier limits or features |
| Slug Uniqueness | Medium | 2-3 days | Not enforced |
| Ownership Transfer | Medium | 3-5 days | Documented but not implemented |
| Cancellation Flow | Medium | 3-5 days | No proper offboarding |
| Suspension Details | Medium | 2-3 days | No reason or auto-reactivation |

#### Recommended Actions

```
Phase 1 (Essential):
├── Convert to JournaledGrain pattern
├── Create OrganizationRegistryGrain for slug uniqueness
├── Add branding settings
├── Implement subscription plan management
└── Add contact information

Phase 2 (Enhanced):
├── Implement ownership transfer
├── Add cancellation workflow
├── Enhance suspension tracking
├── Create multi-site dashboard
├── Add feature flags system
```

#### Key Files
- `/src/DarkVelocity.Host/Domains/Organization/OrganizationGrain.cs`
- `/src/DarkVelocity.Host/Domains/Organization/SiteGrain.cs`
- `/src/DarkVelocity.Host/Domains/Organization/UserGrain.cs`

---

### 17. System Domain

**Completeness: 35%**

#### Current Implementation
- Alert grain with rule engine structure
- Alert types and severity levels
- Email inbox with attachment processing
- Workflow grain (generic state machine)
- Webhook subscription management
- Good test coverage

#### Critical Gaps

| Gap | Priority | Effort | Description |
|-----|----------|--------|-------------|
| NotificationGrain | Critical | 2-3 weeks | INTERFACE ONLY - no implementation |
| Webhook Delivery | Critical | 1-2 weeks | Returns mock success |
| Alert Evaluation | Critical | 1 week | EvaluateRulesAsync() is a stub |
| No Background Jobs | High | 1 week | No scheduled processing |
| Blob Storage | High | 3-5 days | Email attachments not persisted |
| Workflow Rules | Medium | 1-2 weeks | No transition guards |
| Circuit Breaker | Medium | 3-5 days | Webhooks fail permanently |
| Audit Logging | Medium | 1-2 weeks | No dedicated audit grain |

#### Recommended Actions

```
Phase 1 (Critical):
├── Implement NotificationGrain (email, SMS, push)
├── Implement HttpWebhookDeliveryService
├── Implement AlertGrain.EvaluateRulesAsync()
├── Create background job scheduler
└── Add blob storage for attachments

Phase 2 (Reliability):
├── Add webhook retry queue
├── Implement circuit breaker pattern
├── Add notification channels (Slack, Teams)
└── Create workflow transition rules

Phase 3 (Operations):
├── Create audit log grain
├── Add system health monitoring
├── Implement webhook analytics
├── Add workflow SLA tracking
```

#### Key Files
- `/src/DarkVelocity.Host/Domains/Alerts/AlertGrain.cs`
- `/src/DarkVelocity.Host/Domains/Workflow/WebhookGrain.cs`
- `/src/DarkVelocity.Host/Domains/Workflow/EmailInboxGrain.cs`

#### Files to Create
- `/src/DarkVelocity.Host/Domains/Alerts/NotificationGrain.cs`
- `/src/DarkVelocity.Host/Services/HttpWebhookDeliveryService.cs`
- `/src/DarkVelocity.Host/BackgroundServices/AlertEvaluationService.cs`

---

## Implementation Roadmap

### Phase 1: Production Blockers (Weeks 1-8)

| Week | Focus | Deliverables |
|------|-------|--------------|
| 1-2 | Payments | Settlement batch, offline queue |
| 2-3 | Payment Processors | Stripe/Adyen implementation |
| 3-4 | Devices | Offline mode foundation |
| 4-5 | Devices | Printing system |
| 5-6 | External Channels | Deliverect integration |
| 6-7 | System | Notifications, webhooks |
| 7-8 | Fiscal | German compliance MVP |

### Phase 2: Core Completeness (Weeks 9-16)

| Week | Focus | Deliverables |
|------|-------|--------------|
| 9-10 | Finance | Chart of accounts, journal entries |
| 10-11 | Reporting | Labor reports, payment reconciliation |
| 11-12 | Staff | Break tracking, payroll export |
| 12-13 | Orders | Course management, seat assignment |
| 13-14 | Tables & Bookings | Notifications, auto-sync |
| 14-15 | Customers | Segmentation, birthday rewards |
| 15-16 | Costing | API endpoints, dashboards |

### Phase 3: Market Expansion (Weeks 17-24)

| Week | Focus | Deliverables |
|------|-------|--------------|
| 17-18 | External Channels | UberEats, DoorDash |
| 18-20 | Fiscal | Austria, France support |
| 20-22 | Finance | QuickBooks, Xero integration |
| 22-24 | Advanced Features | Analytics, forecasting |

---

## Estimated Total Effort

| Domain | Current | Effort to 90% |
|--------|---------|---------------|
| Payment Processors | 30% | 6-8 weeks |
| Fiscal | 35% | 10-12 weeks |
| System | 35% | 12-17 weeks |
| Finance | 40% | 12-15 weeks |
| External Channels | 40% | 14-16 weeks |
| Devices | 45% | 15-18 weeks |
| Costing | 50% | 6-8 weeks |
| Recipes | 55% | 5-6 weeks |
| Staff | 55% | 8-10 weeks |
| Reporting | 60% | 6-8 weeks |
| Tables & Bookings | 60% | 6-8 weeks |
| Inventory | 60% | 6-8 weeks |
| Customers | 65% | 4-6 weeks |
| Orders | 70% | 4-6 weeks |
| Organization | 70% | 4-5 weeks |
| Payments | 75% | 4-5 weeks |
| Menu | 80% | 3-4 weeks |

**Total Estimated Effort:** 120-160 developer-weeks for full production readiness

---

## Architecture Strengths

The codebase demonstrates strong architectural patterns:

1. **Orleans Virtual Actors** - Proper grain key conventions, single-writer principle
2. **Event Sourcing** - JournaledGrain pattern used consistently where appropriate
3. **HAL+JSON APIs** - Consistent REST API design with hypermedia
4. **Composition over Inheritance** - Grains collaborate via grain-to-grain calls
5. **Stream-Based Integration** - Orleans streams for cross-domain events
6. **Multi-Tenancy** - Proper org/site key prefixing throughout
7. **Test Coverage** - Good test patterns where tests exist

## Architecture Recommendations

1. **Standardize Event Sourcing** - Convert remaining simple-state grains to JournaledGrain
2. **Add Background Services** - Many features need scheduled processing
3. **Implement Circuit Breakers** - For all external integrations
4. **Add Observability** - OpenTelemetry for tracing and metrics
5. **Create Index Grains** - For queryable aggregations
6. **Build Subscriber Pattern** - More Orleans stream subscribers for integration

---

## Appendix: File Index

### Core Domain Files

```
src/DarkVelocity.Host/Domains/
├── Alerts/
│   ├── AlertGrain.cs
│   ├── AlertState.cs
│   └── IAlertGrain.cs
├── Bookings/
│   ├── BookingGrain.cs
│   ├── BookingState.cs
│   └── TableGrain.cs
├── Channels/
│   ├── ChannelGrain.cs
│   ├── ChannelEndpoints.cs
│   └── StatusMappingGrain.cs
├── Costing/
│   └── CostingGrains.cs
├── Customers/
│   ├── CustomerGrain.cs
│   └── LoyaltyProgramGrain.cs
├── Devices/
│   ├── DeviceAuthGrain.cs
│   └── DeviceGrain.cs
├── Finance/
│   ├── AccountGrain.cs
│   └── ExpenseGrain.cs
├── Fiscal/
│   └── FiscalisationGrains.cs
├── Inventory/
│   └── InventoryGrain.cs
├── Menu/
│   ├── MenuCmsGrains.cs
│   └── MenuResolverGrains.cs
├── Orders/
│   ├── OrderGrain.cs
│   └── KitchenGrain.cs
├── Organization/
│   ├── OrganizationGrain.cs
│   ├── SiteGrain.cs
│   └── UserGrain.cs
├── Payments/
│   ├── PaymentGrain.cs
│   └── GiftCardGrain.cs
├── Procurement/
│   └── ProcurementGrains.cs
├── Reporting/
│   └── ReportingGrains.cs
├── Staff/
│   ├── EmployeeGrain.cs
│   └── LaborGrains.cs
└── Workflow/
    ├── WebhookGrain.cs
    ├── WorkflowGrain.cs
    └── EmailInboxGrain.cs
```

### Test Files

```
tests/DarkVelocity.Tests/
├── AlertGrainTests.cs
├── BookingGrainTests.cs
├── ChannelGrainTests.cs
├── CustomerGrainTests.cs
├── DeviceAuthGrainTests.cs
├── EmailInboxGrainTests.cs
├── ExpenseGrainTests.cs
├── FiscalDeviceGrainTests.cs
├── GiftCardGrainTests.cs
├── InventoryGrainTests.cs
├── OrderGrainTests.cs
├── PaymentGrainTests.cs
├── WebhookGrainTests.cs
└── WorkflowGrainTests.cs
```

---

*This document should be updated as domains are completed and new gaps are identified.*
