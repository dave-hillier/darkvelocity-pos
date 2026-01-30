# Inventory & F&B Reporting Implementation Plan

## Executive Summary

This document outlines the implementation plan for a comprehensive F&B (Food & Beverage) reporting product, mapping current capabilities against the target blueprint. The goal is to deliver fast weekly ops views, robust period management accounts, and actionable drill-downs that tie every margin blip back to stock, purchasing, or execution.

---

## Current State vs. Target Blueprint

### Gap Analysis Summary

| Area | Current | Target | Gap | Priority |
|------|---------|--------|-----|----------|
| **Sales Ingestion** | Basic order events | Full item/modifier/discount/tax/tender | Medium | P1 |
| **Inventory Events** | 5 events implemented | 30+ events defined | Large | P1 |
| **Purchasing Integration** | Entity-based CRUD | Event-sourced with 3-way match | Large | P2 |
| **Recipe/Cost Integration** | Basic recipe costs | Recipe versioning with as-of costing | Medium | P1 |
| **Costing Policy** | FIFO only (partial) | FIFO + WAC with policy selection | Medium | P1 |
| **Metric Layer** | None | Daily facts by location × product × channel | Large | P1 |
| **Reporting Surfaces** | DTOs only | Dashboards, alerts, drill-downs | Large | P2 |
| **Event Sourcing** | In-memory events | Persistent event store with replay | Large | P0 |
| **Audit/Governance** | Basic logging | Immutable event log, versioned recipes | Large | P1 |

---

## 1. Event Infrastructure Foundation (P0)

### Current State
- In-memory `IEventBus` with publish/subscribe
- Events not persisted long-term
- No replay capability
- No event versioning

### Target State
- Persistent event store (EventStoreDB or PostgreSQL-backed)
- Append-only, immutable event streams
- Full replay capability for projection rebuilds
- Event versioning and schema evolution

### Implementation Tasks

| Task | Description | Estimate |
|------|-------------|----------|
| **1.1** | Design event store schema (streams, events, snapshots) | |
| **1.2** | Implement `IEventStore` interface with append/read/subscribe | |
| **1.3** | Add event metadata (eventId, occurredAt, emittedAt, source, version) | |
| **1.4** | Implement idempotent event handlers | |
| **1.5** | Build projection infrastructure (streaming + batch rebuild) | |
| **1.6** | Add late-arrival event handling with window reprocessing | |

### Event Base Structure

```csharp
public abstract record DomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public DateTime EmittedAt { get; init; } = DateTime.UtcNow;
    public string Source { get; init; } = string.Empty;
    public int Version { get; init; } = 1;
    public Guid OrgId { get; init; }
    public Guid SiteId { get; init; }
    public abstract string EventType { get; }
}
```

---

## 2. Sales Event Enhancement (P1)

### Current State
- `OrderCreated`, `OrderSettled`, `OrderVoided` events exist
- Basic order line data
- No modifier/discount/tender breakdown
- No channel attribution

### Target State
- Granular sale item events with full financial breakdown
- Recipe expansion at sale time
- Channel and time-of-day attribution
- Void/comp tracking with reasons

### New Events Required

```csharp
// Sales Domain Events
SaleItemAdded
├── CheckId, ItemId, ProductId
├── Quantity, UnitPrice
├── Discounts[], Modifiers[]
├── Timestamp, LocationId, Channel
└── RecipeVersionId (for as-of costing)

SaleItemVoided
├── CheckId, ItemId
├── Reason, ApprovedBy
└── RefundAmount

SaleFinalized
├── CheckId
├── Subtotal, DiscountTotal, TaxTotal, GrandTotal
├── Tenders[] (type, amount, reference)
├── ServerId, TableId, GuestCount
└── OpenedAt, ClosedAt
```

### SalesFact Projection

```csharp
public record SalesFact
{
    public DateTime Date { get; init; }
    public Guid LocationId { get; init; }
    public string Channel { get; init; }  // dine-in, takeout, delivery
    public Guid ProductId { get; init; }
    public int Quantity { get; init; }
    public decimal GrossSales { get; init; }
    public decimal Discounts { get; init; }
    public decimal Voids { get; init; }
    public decimal Comps { get; init; }
    public decimal Tax { get; init; }
    public decimal NetSales { get; init; }
    public Guid CheckId { get; init; }
}
```

---

## 3. Inventory Event Completion (P1)

### Current State (5 events implemented)
- `StockBatchCreated`
- `StockBatchConsumed`
- `StockBatchExhausted`
- `StockConsumedForSale`
- `RecipeCostRecalculated`

### Target State (30+ events from event storming)

### Missing Events by Category

#### Receipt Events
| Event | Status | Gap |
|-------|--------|-----|
| `StockBatchReceived` | Partially implemented | Add supplier/delivery links |
| `DeliveryReceived` | Not implemented | Full delivery integration |
| `TransferReceived` | Not implemented | Inter-site transfers |
| `ReceiptAdjusted` | Not implemented | Correction workflow |
| `BatchRejected` | Not implemented | Quality rejection |

#### Consumption Events
| Event | Status | Gap |
|-------|--------|-----|
| `StockConsumed` | Exists | Needs batch breakdown |
| `StockConsumedForOrder` | Exists | Add theoretical comparison |
| `StockWasted` | Not implemented | Waste tracking |
| `StockSampled` | Not implemented | Sample tracking |
| `StockTransferredOut` | Not implemented | Transfer support |
| `ConsumptionReversed` | Not implemented | Correction support |

#### Batch Lifecycle Events
| Event | Status | Gap |
|-------|--------|-----|
| `BatchExhausted` | Exists | Complete |
| `BatchExpired` | Not implemented | Expiry monitoring |
| `BatchWrittenOff` | Not implemented | Expiry write-off |
| `BatchesMerged` | Not implemented | Batch consolidation |
| `StockFrozen` | Not implemented | Freeze tracking |
| `StockDefrosted` | Not implemented | Thaw tracking |
| `ContainerUnpacked` | Not implemented | Container to batch |

#### Level Monitoring Events
| Event | Status | Gap |
|-------|--------|-----|
| `LowStockAlertTriggered` | Not implemented | Alert system |
| `OutOfStockAlertTriggered` | Not implemented | Alert system |
| `StockLevelNormalized` | Not implemented | Alert clearing |
| `ParLevelExceeded` | Not implemented | Overstocking alert |

#### Adjustment Events
| Event | Status | Gap |
|-------|--------|-----|
| `QuantityAdjusted` | Not implemented | Count correction |
| `PhysicalCountRecorded` | Not implemented | Stock takes |
| `CostAdjusted` | Not implemented | Cost correction |
| `WeightedAverageCostRecalculated` | Not implemented | WAC support |

#### Transfer Events
| Event | Status | Gap |
|-------|--------|-----|
| `TransferInitiated` | Not implemented | Inter-site transfers |
| `TransferSent` | Not implemented | Transit tracking |
| `TransferCompleted` | Not implemented | Completion |
| `TransferCancelled` | Not implemented | Abort handling |

---

## 4. Costing Policy Implementation (P1)

### Current State
- Basic FIFO consumption via `IFifoConsumptionService`
- No weighted average cost (WAC) recalculation
- No policy selection mechanism
- Recipe costs calculated but not linked to consumption

### Target State

#### Dual Costing Policy Support

| Policy | Use Case | Calculation |
|--------|----------|-------------|
| **FIFO** | Ops GP (weekly) | Actual batch costs consumed |
| **WAC** | Management GP (monthly) | Rolling weighted average |
| **Standard** | Menu costing | Latest supplier/invoice cost |

### Implementation

```csharp
public interface ICostingPolicy
{
    string PolicyName { get; }
    Task<ConsumptionCost> CalculateCostAsync(
        Guid ingredientId,
        decimal quantity,
        DateTime asOfDate);
}

public class FifoCostingPolicy : ICostingPolicy
{
    // Consume from oldest batches first
    // Track actual batch costs used
}

public class WeightedAverageCostingPolicy : ICostingPolicy
{
    // Use WAC at time of consumption
    // WAC = (existing qty × existing WAC + new qty × new cost) / total qty
}

public class StandardCostingPolicy : ICostingPolicy
{
    // Use latest supplier price from catalog
    // For menu engineering and pricing decisions
}
```

### WAC Recalculation Event

```csharp
public record WeightedAverageCostRecalculated : DomainEvent
{
    public override string EventType => "inventory.stock.wac_recalculated";

    public required Guid IngredientId { get; init; }
    public required Guid SiteId { get; init; }
    public required decimal PreviousWAC { get; init; }
    public required decimal NewWAC { get; init; }
    public required decimal TriggeringBatchQty { get; init; }
    public required decimal TriggeringBatchCost { get; init; }
    public required decimal TotalQuantity { get; init; }
    public required decimal TotalValue { get; init; }
}
```

---

## 5. Recipe Version & Theoretical Cost (P1)

### Current State
- Recipes exist with ingredients and costs
- `RecipeCostSnapshot` for historical tracking
- No formal versioning
- No as-of-sale recipe lookup
- No theoretical vs actual comparison

### Target State

#### Recipe Versioning

```csharp
public record RecipeVersionPublished : DomainEvent
{
    public override string EventType => "menu.recipe.version_published";

    public required Guid ProductId { get; init; }
    public required Guid RecipeVersionId { get; init; }
    public required int VersionNumber { get; init; }
    public required IReadOnlyList<RecipeIngredient> Ingredients { get; init; }
    public required IReadOnlyList<Allergen> Allergens { get; init; }
    public required IReadOnlyList<DietaryFlag> DietaryFlags { get; init; }
    public required decimal TheoreticalCost { get; init; }
    public required decimal PortionYield { get; init; }
    public required DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public required Guid PublishedBy { get; init; }
}

public record RecipeIngredient
{
    public Guid IngredientId { get; init; }
    public string IngredientName { get; init; }
    public decimal Quantity { get; init; }
    public string Unit { get; init; }
    public decimal WastePercentage { get; init; }
    public decimal EffectiveQuantity { get; init; } // After waste
    public decimal StandardCost { get; init; }
}
```

#### Theoretical vs Actual COGS

```csharp
public record ConsumptionFact
{
    public DateTime Date { get; init; }
    public Guid LocationId { get; init; }
    public Guid ProductId { get; init; }

    // Theoretical (from recipes)
    public decimal TheoreticalQty { get; init; }
    public decimal TheoreticalCost { get; init; }

    // Actual (from inventory movements)
    public decimal ActualQty { get; init; }
    public decimal ActualCost { get; init; }

    // Variance
    public decimal VarianceQty { get; init; }
    public decimal VarianceCost { get; init; }
    public decimal VariancePercent { get; init; }

    public Guid? BatchId { get; init; }
    public string CostingMethod { get; init; } // FIFO, WAC
}
```

---

## 6. Waste & Compliance Tracking (P1)

### Current State
- `WasteRecord` entity exists
- `WasteRecordDto` defined
- No waste events
- No reason categorization
- No cost attribution

### Target State

#### Waste Events

```csharp
public record WasteRecorded : DomainEvent
{
    public override string EventType => "inventory.waste.recorded";

    public required Guid WasteId { get; init; }
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public Guid? BatchId { get; init; }
    public required decimal Quantity { get; init; }
    public required string Unit { get; init; }
    public required WasteReason Reason { get; init; }
    public required string ReasonDetails { get; init; }
    public required decimal CostBasis { get; init; }
    public required Guid RecordedBy { get; init; }
    public Guid? ApprovedBy { get; init; }
    public string? PhotoUrl { get; init; }
}

public enum WasteReason
{
    Spoilage,
    Expired,
    LineCleaning,   // Draught lines
    Breakage,
    OverProduction,
    CustomerReturn,
    QualityRejection,
    SpillageAccident,
    Theft,
    Other
}
```

#### WasteFact Projection

```csharp
public record WasteFact
{
    public DateTime Date { get; init; }
    public Guid LocationId { get; init; }
    public Guid ProductId { get; init; }
    public Guid? SkuId { get; init; }
    public decimal Quantity { get; init; }
    public WasteReason Reason { get; init; }
    public decimal CostBasis { get; init; }

    // Derived
    public decimal WasteRate { get; init; } // vs purchases or net sales
}
```

#### Yield Tracking (Kegs, Roasts)

```csharp
public record YieldRecorded : DomainEvent
{
    public override string EventType => "inventory.yield.recorded";

    public required Guid BatchId { get; init; }
    public required decimal ExpectedYield { get; init; }
    public required decimal ActualYield { get; init; }
    public required decimal YieldPercentage { get; init; }
    public required decimal Variance { get; init; }
    public string? Notes { get; init; }
}
```

---

## 7. Purchasing & Supplier Performance (P2)

### Current State
- `Supplier`, `PurchaseOrder`, `Delivery` entities
- Basic CRUD controllers
- No three-way match
- No supplier performance metrics
- No price tracking

### Target State

#### Three-Way Match Implementation

```csharp
public record ThreeWayMatchPerformed : DomainEvent
{
    public override string EventType => "procurement.match.performed";

    public required Guid InvoiceId { get; init; }
    public required Guid PurchaseOrderId { get; init; }
    public required Guid DeliveryId { get; init; }
    public required MatchStatus Status { get; init; }

    // Comparisons
    public required decimal POQty { get; init; }
    public required decimal DeliveryQty { get; init; }
    public required decimal InvoiceQty { get; init; }

    public required decimal POAmount { get; init; }
    public required decimal DeliveryAmount { get; init; }
    public required decimal InvoiceAmount { get; init; }

    public IReadOnlyList<MatchDiscrepancy>? Discrepancies { get; init; }
}

public enum MatchStatus
{
    ExactMatch,
    WithinTolerance,
    QuantityMismatch,
    PriceMismatch,
    RequiresReview
}
```

#### PurchaseFact Projection

```csharp
public record PurchaseFact
{
    public DateTime Date { get; init; }
    public Guid SupplierId { get; init; }
    public Guid SkuId { get; init; }
    public Guid ProductId { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitCost { get; init; }
    public decimal TotalCost { get; init; }
    public int LeadTimeDays { get; init; }
    public bool OnTime { get; init; }
    public decimal? PriceVariance { get; init; } // vs last invoice
}
```

#### Supplier Performance Metrics

| Metric | Formula | Target |
|--------|---------|--------|
| **On-Time Delivery %** | On-time deliveries ÷ Total deliveries | > 95% |
| **Invoice Match Rate** | Exact matches ÷ Total invoices | > 98% |
| **Quality Acceptance Rate** | Accepted qty ÷ Delivered qty | > 99% |
| **Lead-time SLA** | Avg actual lead time vs quoted | Within 1 day |
| **Price Movement Index** | Current price ÷ Baseline price | Track trend |

---

## 8. Inventory Fact Projections (P1)

### Current State
- `StockLevelDto` returns current levels
- No end-of-day snapshots
- No historical tracking
- No batch-level projections

### Target State

#### InventoryFact (Daily Snapshot)

```csharp
public record InventoryFact
{
    public DateTime Date { get; init; }
    public Guid LocationId { get; init; }
    public Guid SkuId { get; init; }
    public Guid? BatchId { get; init; }

    public decimal OnHandQty { get; init; }
    public decimal ReservedQty { get; init; }
    public decimal AvailableQty { get; init; }

    public decimal Value { get; init; }  // Based on costing policy
    public string CostingMethod { get; init; }

    public DateTime? ExpiryDate { get; init; }
    public FreezeState FreezeState { get; init; }

    // Derived
    public int DaysOnHand { get; init; }
    public bool IsLowStock { get; init; }
    public bool IsExpiringSoon { get; init; }
}

public enum FreezeState
{
    Fresh,
    Frozen,
    Defrosted
}
```

#### Stock Health Metrics

| Metric | Formula | Alert Threshold |
|--------|---------|-----------------|
| **Stock Turn** | COGS ÷ Avg stock value | < 4 per month |
| **Days on Hand** | Closing value ÷ (COGS / days) | > 14 days |
| **PAR Compliance** | Items at PAR ÷ Total items | < 80% |
| **Expiry Risk** | Value expiring in 7 days | > 5% of stock |
| **Aged Stock** | Value of batches > 30 days | > 10% of stock |

---

## 9. Metric Layer & Aggregations (P1)

### Period Definitions

| Period | Scope | Costing | Use Case |
|--------|-------|---------|----------|
| **Daily** | Location × Product × Channel | FIFO | Ops review |
| **Weekly** (Mon-Sun) | Location × Product | FIFO | Ops GP |
| **4-Week / 13-Period** | Location × Category | WAC | Management GP |
| **Monthly** | Org × Location | WAC | Financial reporting |

### Core Metrics Implementation

#### Sales & Revenue

```csharp
public record SalesMetrics
{
    public decimal GrossSales { get; init; }
    public decimal Discounts { get; init; }
    public decimal Voids { get; init; }
    public decimal Comps { get; init; }
    public decimal Tax { get; init; }
    public decimal NetSales { get; init; }  // Gross - discounts - voids - comps - tax

    public int TransactionCount { get; init; }
    public decimal ATV { get; init; }  // Net sales ÷ transactions

    public int CoversServed { get; init; }
    public decimal RevenuePerCover { get; init; }
}
```

#### Cost & Gross Profit

```csharp
public record GrossProfitMetrics
{
    // Actual (from inventory movements)
    public decimal ActualCOGS { get; init; }
    public decimal ActualCOGSPercent { get; init; }
    public decimal ActualGP { get; init; }
    public decimal ActualGPPercent { get; init; }

    // Theoretical (from recipes)
    public decimal TheoreticalCOGS { get; init; }
    public decimal TheoreticalCOGSPercent { get; init; }
    public decimal TheoreticalGP { get; init; }
    public decimal TheoreticalGPPercent { get; init; }

    // Variance
    public decimal Variance { get; init; }
    public decimal VariancePercent { get; init; }

    public string CostingPolicy { get; init; } // FIFO, WAC
}
```

#### Variance Analysis

```csharp
public record VarianceBreakdown
{
    public Guid IngredientId { get; init; }
    public string IngredientName { get; init; }

    public decimal TheoreticalUsage { get; init; }
    public decimal ActualUsage { get; init; }
    public decimal UsageVariance { get; init; }

    public decimal TheoreticalCost { get; init; }
    public decimal ActualCost { get; init; }
    public decimal CostVariance { get; init; }

    public VarianceReason LikelyReason { get; init; }
}

public enum VarianceReason
{
    OverPour,
    Waste,
    Theft,
    PortionControl,
    RecipeNotFollowed,
    CountError,
    Unknown
}
```

---

## 10. Reporting Surfaces (P2)

### Dashboard Requirements

#### GM Daily Dashboard
- Yesterday's net sales vs LW, LY
- GP% actual vs budget
- Top 5 variance items
- Low stock alerts count
- Outstanding PO value

#### Kitchen/Bar Lead Dashboard
- Par level compliance
- Expiry risk items
- Waste recorded today
- Stock outs impacting menu

#### Finance Dashboard
- Period-to-date GP by location
- Variance drill-down to product
- COGS trend (13 periods)
- AP aging by supplier

### Alert System

```csharp
public record Alert
{
    public AlertType Type { get; init; }
    public AlertSeverity Severity { get; init; }
    public string Title { get; init; }
    public string Message { get; init; }
    public Guid? EntityId { get; init; }
    public string EntityType { get; init; }
    public DateTime TriggeredAt { get; init; }
    public bool IsAcknowledged { get; init; }
}

public enum AlertType
{
    GPDropped,
    HighVariance,
    LowStock,
    OutOfStock,
    ExpiryRisk,
    SupplierPriceSpike,
    NegativeStock,
    ParExceeded
}
```

#### Alert Rules

| Alert | Trigger | Severity |
|-------|---------|----------|
| GP% Drop | > 3pts vs LW | High |
| High Variance | > 15% actual vs theoretical | Medium |
| Under-PAR | Stock < reorder point | Medium |
| Expiry Risk | < 3 days to expiry | High |
| Price Spike | > 10% vs last invoice | Medium |
| Negative Stock | qty < 0 | Critical |

---

## 11. Menu Engineering (P2)

### Contribution Margin Analysis

```csharp
public record MenuItemAnalysis
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; }
    public string Category { get; init; }

    public decimal SellingPrice { get; init; }
    public decimal TheoreticalCost { get; init; }
    public decimal ContributionMargin { get; init; }
    public decimal ContributionMarginPercent { get; init; }

    public int UnitsSold { get; init; }
    public decimal TotalContribution { get; init; }
    public decimal MenuMix { get; init; }  // Units ÷ category units

    public MenuClass Classification { get; init; }
}

public enum MenuClass
{
    Star,       // High margin, high popularity
    Plowhorse,  // Low margin, high popularity
    Puzzle,     // High margin, low popularity
    Dog         // Low margin, low popularity
}
```

### Classification Thresholds

```
              High Popularity
                    │
         Plowhorse  │  Star
    ────────────────┼────────────────
           Dog      │  Puzzle
                    │
              Low Popularity
         Low Margin   High Margin
```

---

## 12. Data Model Summary

### Fact Tables (Projections)

| Fact | Grain | Source Events |
|------|-------|---------------|
| **SalesFact** | date × location × channel × product | SaleItemAdded, SaleFinalized |
| **ConsumptionFact** | date × location × product × method | StockConsumed, recipe expansion |
| **InventoryFact** | date × location × sku × batch | All inventory events (EOD snapshot) |
| **PurchaseFact** | date × supplier × sku | DeliveryReceived, InvoiceCaptured |
| **WasteFact** | date × location × product × reason | WasteRecorded |

### Dimension Tables (from Events)

| Dimension | Source | SCD Type |
|-----------|--------|----------|
| **Product** | ProductCreated, ProductUpdated | Type 2 |
| **Recipe** | RecipeVersionPublished | Type 2 |
| **Supplier** | SupplierCreated, SupplierUpdated | Type 1 |
| **Location** | LocationCreated, LocationUpdated | Type 1 |
| **Container** | ContainerCreated | Type 1 |

---

## 13. Implementation Phases

### Phase 1: Event Foundation (Weeks 1-4)

**Goal**: Establish persistent event store and projection infrastructure

| Week | Deliverables |
|------|--------------|
| 1 | Event store schema and `IEventStore` interface |
| 2 | Projection infrastructure with rebuild capability |
| 3 | Migrate existing events to new format with metadata |
| 4 | Integration testing of event replay |

### Phase 2: Inventory Core (Weeks 5-8)

**Goal**: Complete inventory event coverage

| Week | Deliverables |
|------|--------------|
| 5 | Implement missing receipt events (delivery, transfer) |
| 6 | Implement consumption events with batch breakdown |
| 7 | Implement adjustment and stock take events |
| 8 | Build InventoryFact daily snapshot projection |

### Phase 3: Costing & Variance (Weeks 9-12)

**Goal**: Dual costing policies and variance tracking

| Week | Deliverables |
|------|--------------|
| 9 | Implement WAC recalculation on receipts |
| 10 | Implement FIFO consumption with batch ledger |
| 11 | Recipe versioning with as-of lookup |
| 12 | ConsumptionFact with theoretical vs actual |

### Phase 4: Reporting (Weeks 13-16)

**Goal**: Dashboards and alerts

| Week | Deliverables |
|------|--------------|
| 13 | SalesFact projection and daily aggregations |
| 14 | GP metrics calculation (FIFO and WAC) |
| 15 | Alert rule engine and notifications |
| 16 | Dashboard API endpoints |

### Phase 5: Procurement Integration (Weeks 17-20)

**Goal**: Three-way match and supplier metrics

| Week | Deliverables |
|------|--------------|
| 17 | Procurement events migration |
| 18 | Three-way match implementation |
| 19 | PurchaseFact and supplier performance |
| 20 | Price tracking and alerts |

---

## 14. Critical Design Decisions

### Event Time vs Processing Time
- Use `occurredAt` for business truth
- Use `emittedAt` for system debugging
- Handle late arrivals by reprocessing affected time windows
- Never mutate projected facts; append adjustments instead

### Period Close
- Snapshot projection states at period end
- Late events create adjustment rows
- Period reports are point-in-time immutable
- Adjustments flow to next period

### Costing Policy Selection
- **Ops GP (Weekly)**: FIFO batch costs, include waste
- **Management GP (Monthly)**: WAC, normalized for period
- **Menu Costing**: Latest supplier price (standard cost)

### Grain Decisions
- SalesFact: product × day × location × channel
- ConsumptionFact: product × day × location × method
- InventoryFact: sku × batch × day × location
- Keep detailed line grain for drill-through

---

## 15. Risk Register

| Risk | Impact | Mitigation |
|------|--------|------------|
| Event schema evolution breaks projections | High | Version events, backward-compatible changes |
| FIFO calculation performance | Medium | Batch ledger with running balance |
| Late-arriving events | Medium | Reprocessing windows, adjustment rows |
| Cross-site data consistency | Medium | Event ordering by `occurredAt` |
| Large event volume | Medium | Partitioning by org/site, archival policy |

---

## 16. Success Criteria

### Phase 1 Exit Criteria
- [ ] Events persist with full metadata
- [ ] Projections rebuild from scratch successfully
- [ ] Event replay is deterministic

### Phase 2 Exit Criteria
- [ ] All 30+ inventory events implemented
- [ ] InventoryFact matches manual stock count within 0.1%
- [ ] Batch traceability complete

### Phase 3 Exit Criteria
- [ ] FIFO and WAC produce correct GP
- [ ] Variance < 0.5% vs expected
- [ ] Recipe versions track correctly

### Phase 4 Exit Criteria
- [ ] Dashboard shows correct metrics
- [ ] Alerts fire within 5 minutes
- [ ] Drill-down to event level works

### Phase 5 Exit Criteria
- [ ] Three-way match catches discrepancies
- [ ] Supplier performance accurate
- [ ] Price trends visible

---

## Appendix A: Event Stream Partitioning

```
Events are partitioned by: {orgId}:{siteId}:{domain}

Examples:
- org123:site456:inventory
- org123:site456:sales
- org123:site456:procurement

Global streams (org-level):
- org123:_:suppliers
- org123:_:products
- org123:_:recipes
```

## Appendix B: Projection Rebuild Process

```
1. Mark projection as "rebuilding"
2. Clear projection tables (or use shadow tables)
3. Replay events from stream start
4. Apply each event through projector
5. Mark projection as "active"
6. Switch traffic to new tables
```

## Appendix C: Alert Notification Channels

| Channel | Use Case | Latency |
|---------|----------|---------|
| Slack | Ops alerts | < 1 min |
| Email | Daily digests | Batch |
| Push | Critical alerts | < 30 sec |
| Dashboard | All alerts | Real-time |
