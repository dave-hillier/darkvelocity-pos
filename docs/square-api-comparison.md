# Square API vs DarkVelocity POS API Comparison

This document compares the Square API with the DarkVelocity POS API to identify gaps, alignment opportunities, and architectural differences.

## Executive Summary

| Aspect | Square API | DarkVelocity POS API |
|--------|------------|---------------------|
| **API Style** | REST, JSON | REST, HAL+JSON |
| **Authentication** | OAuth 2.0 | OAuth 2.0, Device Flow (RFC 8628), PIN |
| **Versioning** | URL path (`/v2/`) | Not versioned (planned) |
| **Maturity** | Production-ready, comprehensive | Early stage, most endpoints disabled |

---

## API Domain Coverage

### Square API Domains

| Domain | Square API | DarkVelocity Status | Notes |
|--------|------------|---------------------|-------|
| **Locations** | ✅ Full | ⚠️ Disabled | Square: sites/stores; DV: Organizations + Sites |
| **Catalog** | ✅ Full | ⚠️ Disabled | Square: items, variations, modifiers, categories; DV: Menu API |
| **Orders** | ✅ Full | ⚠️ Disabled | Core POS functionality |
| **Payments** | ✅ Full | ⚠️ Disabled | Payment processing |
| **Checkout** | ✅ Full | ❌ Not planned | Hosted checkout pages |
| **Inventory** | ✅ Full | ⚠️ Disabled | Stock tracking |
| **Customers** | ✅ Full | ⚠️ Disabled | Customer profiles |
| **Team/Labor** | ✅ Full | ⚠️ Disabled | Employee management |
| **Devices** | ✅ Terminal API | ✅ Active | Device management |
| **OAuth** | ✅ Full | ✅ Active | Authentication |

---

## Detailed API Comparison

### 1. Locations / Sites

#### Square Locations API
```
GET    /v2/locations                    # List all locations
POST   /v2/locations                    # Create location
GET    /v2/locations/{location_id}      # Get location
PUT    /v2/locations/{location_id}      # Update location
```

**Square Location Object:**
```json
{
  "id": "LOCATION_ID",
  "name": "Main Store",
  "address": { "address_line_1": "...", "locality": "...", "country": "US" },
  "timezone": "America/Los_Angeles",
  "currency": "USD",
  "business_hours": { "periods": [...] },
  "status": "ACTIVE",
  "merchant_id": "MERCHANT_ID"
}
```

#### DarkVelocity Sites (Disabled)
```
POST   /api/locations/organizations         # Create organization
GET    /api/locations/organizations/{orgId} # Get organization
POST   /api/locations/sites                 # Create site
GET    /api/locations/sites/{orgId}/{siteId} # Get site
```

**Comparison:**
| Feature | Square | DarkVelocity |
|---------|--------|--------------|
| Multi-tenant hierarchy | Merchant → Locations | Organization → Sites |
| Timezone support | ✅ | ✅ |
| Currency per location | ✅ | ✅ |
| Business hours | ✅ | ❌ Not implemented |
| Address/Coordinates | ✅ | ⚠️ Planned |
| Tax jurisdiction | ⚠️ Via tax settings | ✅ Per site |

**Gap:** DarkVelocity needs business hours support for sites.

---

### 2. Catalog / Menu

#### Square Catalog API
```
GET    /v2/catalog/list                     # List catalog objects
POST   /v2/catalog/search                   # Search with filters
POST   /v2/catalog/object                   # Upsert single object
POST   /v2/catalog/batch-upsert             # Batch upsert
POST   /v2/catalog/batch-retrieve           # Batch retrieve
POST   /v2/catalog/batch-delete             # Batch delete
POST   /v2/catalog/update-item-modifier-lists  # Update modifiers
GET    /v2/catalog/object/{object_id}       # Get by ID
DELETE /v2/catalog/object/{object_id}       # Delete by ID
POST   /v2/catalog/search-catalog-items     # Search items specifically
```

**Square CatalogObject Types:**
- `ITEM` - Base product
- `ITEM_VARIATION` - Specific variant with price/SKU
- `CATEGORY` - Grouping for items
- `MODIFIER_LIST` - Set of modifiers (e.g., sizes, add-ons)
- `MODIFIER` - Individual modifier option
- `DISCOUNT` - Discount definitions
- `TAX` - Tax definitions
- `IMAGE` - Product images

**Square Catalog Item Structure:**
```json
{
  "type": "ITEM",
  "id": "ITEM_ID",
  "item_data": {
    "name": "Coffee",
    "description": "Fresh brewed",
    "category_id": "CATEGORY_ID",
    "variations": [
      {
        "type": "ITEM_VARIATION",
        "id": "VAR_SMALL",
        "item_variation_data": {
          "name": "Small",
          "pricing_type": "FIXED_PRICING",
          "price_money": { "amount": 250, "currency": "USD" },
          "sku": "COFFEE-SM"
        }
      }
    ],
    "modifier_list_info": [
      {
        "modifier_list_id": "MILK_OPTIONS",
        "min_selected_modifiers": 0,
        "max_selected_modifiers": 1
      }
    ]
  }
}
```

#### DarkVelocity Menu API (Disabled)
```
POST   /api/menu/items                        # Create menu item
GET    /api/menu/items/{orgId}/{menuItemId}   # Get menu item
PUT    /api/menu/items/{orgId}/{menuItemId}/price  # Update price
POST   /api/menu/categories                   # Create category
POST   /api/menu/modifiers                    # Create modifier
```

**Comparison:**
| Feature | Square | DarkVelocity |
|---------|--------|--------------|
| Item variations | ✅ First-class | ⚠️ Implicit via modifiers |
| Modifier lists | ✅ Reusable across items | ⚠️ Per-item |
| Min/max modifier selection | ✅ | ❌ |
| Batch operations | ✅ | ❌ |
| Search/filter | ✅ Advanced | ⚠️ Basic |
| Images | ✅ Native | ❌ |
| SKU support | ✅ | ⚠️ Planned |
| Tax assignment | ✅ Per item | ⚠️ Per site |

**Gaps:**
1. DarkVelocity needs item variations as first-class concept
2. Missing batch upsert/retrieve operations
3. No modifier list reusability
4. No catalog search/filter endpoints

---

### 3. Orders

#### Square Orders API
```
POST   /v2/orders                      # Create order
POST   /v2/orders/batch-retrieve       # Batch retrieve
POST   /v2/orders/search               # Search orders
GET    /v2/orders/{order_id}           # Get order
PUT    /v2/orders/{order_id}           # Update order
POST   /v2/orders/{order_id}/pay       # Pay for order
POST   /v2/orders/calculate            # Calculate totals (preview)
POST   /v2/orders/clone                # Clone an order
```

**Square Order Object:**
```json
{
  "idempotency_key": "unique-key",
  "order": {
    "location_id": "LOCATION_ID",
    "reference_id": "my-order-001",
    "customer_id": "CUSTOMER_ID",
    "line_items": [
      {
        "quantity": "1",
        "catalog_object_id": "ITEM_VAR_ID",
        "modifiers": [
          { "catalog_object_id": "MOD_ID" }
        ],
        "applied_discounts": [
          { "discount_uid": "discount-1" }
        ]
      }
    ],
    "taxes": [
      {
        "uid": "tax-1",
        "name": "Sales Tax",
        "percentage": "8.5",
        "scope": "ORDER"
      }
    ],
    "discounts": [
      {
        "uid": "discount-1",
        "name": "10% Off",
        "percentage": "10",
        "scope": "LINE_ITEM"
      }
    ],
    "fulfillments": [
      {
        "type": "PICKUP",
        "state": "PROPOSED",
        "pickup_details": {
          "pickup_at": "2024-01-15T12:00:00Z"
        }
      }
    ],
    "state": "OPEN"
  }
}
```

**Square Order States:**
- `DRAFT` - Not finalized
- `OPEN` - Active, accepting modifications
- `COMPLETED` - Fully paid
- `CANCELED` - Canceled

#### DarkVelocity Orders API (Disabled)
```
POST   /api/orders/                              # Create order
GET    /api/orders/{orgId}/{orderId}             # Get order
POST   /api/orders/{orgId}/{orderId}/items       # Add item
POST   /api/orders/{orgId}/{orderId}/submit      # Submit order
POST   /api/orders/{orgId}/{orderId}/complete    # Complete order
POST   /api/orders/{orgId}/{orderId}/cancel      # Cancel order
```

**Comparison:**
| Feature | Square | DarkVelocity |
|---------|--------|--------------|
| Order creation | ✅ Full order in one call | ✅ |
| Incremental updates | ✅ PUT with version | ⚠️ Via events |
| Order search | ✅ Rich filters | ❌ |
| Order calculate/preview | ✅ | ❌ |
| Clone order | ✅ | ❌ |
| Fulfillments | ✅ (PICKUP, SHIPMENT, DELIVERY) | ⚠️ Kitchen tickets |
| Idempotency | ✅ Required | ⚠️ Optional |
| Discounts | ✅ Line-item & order level | ⚠️ Planned |
| Tips | ✅ | ❌ |
| Service charges | ✅ | ❌ |

**Gaps:**
1. No order search endpoint
2. No order preview/calculate
3. Missing fulfillment types (delivery, shipment)
4. No service charges support

---

### 4. Payments

#### Square Payments API
```
POST   /v2/payments                    # Create payment
GET    /v2/payments/{payment_id}       # Get payment
PUT    /v2/payments/{payment_id}       # Update payment
POST   /v2/payments/{payment_id}/cancel   # Cancel payment
POST   /v2/payments/{payment_id}/complete # Complete payment
GET    /v2/payments                    # List payments
```

**Square Payment Object:**
```json
{
  "source_id": "PAYMENT_TOKEN",
  "idempotency_key": "unique-key",
  "amount_money": {
    "amount": 1000,
    "currency": "USD"
  },
  "tip_money": {
    "amount": 200,
    "currency": "USD"
  },
  "app_fee_money": {
    "amount": 50,
    "currency": "USD"
  },
  "order_id": "ORDER_ID",
  "customer_id": "CUSTOMER_ID",
  "location_id": "LOCATION_ID",
  "reference_id": "my-payment-001",
  "autocomplete": true
}
```

**Square Payment Methods:**
- Cards (credit, debit)
- Cash App Pay
- Google Pay
- Apple Pay
- Afterpay/Clearpay
- Bank transfers (ACH)
- Gift cards

#### DarkVelocity Payments API (Disabled)
```
POST   /api/payments/                              # Create payment
GET    /api/payments/{orgId}/{paymentId}           # Get payment
POST   /api/payments/{orgId}/{paymentId}/process   # Process payment
POST   /api/payments/refunds                       # Create refund
```

**Comparison:**
| Feature | Square | DarkVelocity |
|---------|--------|--------------|
| Card payments | ✅ | ⚠️ Via gateway |
| Cash payments | ✅ | ⚠️ Planned |
| Split payments | ✅ | ⚠️ Planned |
| Tips | ✅ | ❌ |
| Refunds | ✅ Full & partial | ⚠️ Basic |
| Payment search | ✅ | ❌ |
| Delayed capture | ✅ | ❌ |
| App fees | ✅ | ❌ |

**Gaps:**
1. No native payment processing (relies on external gateway)
2. Missing tip support
3. No delayed capture
4. No payment search

---

### 5. Inventory

#### Square Inventory API
```
GET    /v2/inventory/{catalog_object_id}           # Get inventory count
POST   /v2/inventory/batch-retrieve-counts         # Batch retrieve
POST   /v2/inventory/batch-change                  # Batch adjust
POST   /v2/inventory/batch-retrieve-changes        # Get change history
POST   /v2/inventory/physical-count                # Record physical count
POST   /v2/inventory/transfer                      # Transfer between locations
```

**Square Inventory States:**
- `IN_STOCK` - Available for sale
- `SOLD` - Sold to customer
- `RETURNED_BY_CUSTOMER` - Returned
- `WASTE` - Spoiled/damaged
- `IN_TRANSIT` - Being transferred
- `NONE` - Not tracked

**Square Inventory Change:**
```json
{
  "idempotency_key": "unique-key",
  "changes": [
    {
      "type": "ADJUSTMENT",
      "adjustment": {
        "location_id": "LOCATION_ID",
        "catalog_object_id": "ITEM_VAR_ID",
        "from_state": "NONE",
        "to_state": "IN_STOCK",
        "quantity": "10",
        "occurred_at": "2024-01-15T10:00:00Z"
      }
    }
  ]
}
```

#### DarkVelocity Inventory API (Disabled)
```
POST   /api/inventory/items                            # Create inventory item
GET    /api/inventory/items/{orgId}/{itemId}           # Get inventory
POST   /api/inventory/items/{orgId}/{itemId}/adjust    # Adjust inventory
POST   /api/inventory/counts                           # Create count
```

**Comparison:**
| Feature | Square | DarkVelocity |
|---------|--------|--------------|
| Inventory states | ✅ Multiple states | ⚠️ Basic |
| Batch operations | ✅ | ❌ |
| Transfer between sites | ✅ | ⚠️ Planned |
| Change history | ✅ | ⚠️ Via events |
| Physical counts | ✅ | ⚠️ Basic |
| Auto-update from orders | ✅ | ⚠️ Planned |

**Gaps:**
1. No batch inventory operations
2. Limited inventory state model
3. No transfer endpoint

---

### 6. Customers

#### Square Customers API
```
POST   /v2/customers                         # Create customer
GET    /v2/customers/{customer_id}           # Get customer
PUT    /v2/customers/{customer_id}           # Update customer
DELETE /v2/customers/{customer_id}           # Delete customer
POST   /v2/customers/search                  # Search customers
POST   /v2/customers/groups                  # Create group
POST   /v2/customers/{id}/groups/{group_id}  # Add to group
```

**Square Customer Object:**
```json
{
  "given_name": "John",
  "family_name": "Doe",
  "email_address": "john@example.com",
  "phone_number": "+1-555-555-5555",
  "address": { ... },
  "birthday": "1990-01-15",
  "reference_id": "my-customer-001",
  "note": "VIP customer",
  "preferences": {
    "email_unsubscribed": false
  }
}
```

#### DarkVelocity Customers API (Disabled)
```
POST   /api/customers/                           # Create customer
GET    /api/customers/{orgId}/{customerId}       # Get customer
PUT    /api/customers/{orgId}/{customerId}       # Update customer
POST   /api/customers/search                     # Search customers
```

**Comparison:**
| Feature | Square | DarkVelocity |
|---------|--------|--------------|
| Basic CRUD | ✅ | ⚠️ Disabled |
| Search | ✅ Advanced | ⚠️ Basic |
| Customer groups | ✅ | ❌ |
| Preferences | ✅ | ❌ |
| Loyalty integration | ✅ | ❌ |
| Purchase history | ✅ | ⚠️ Via orders |

---

### 7. Devices / Terminal

#### Square Terminal API
```
POST   /v2/devices/codes                     # Create device code
GET    /v2/devices/codes/{id}                # Get device code
GET    /v2/devices                           # List devices
GET    /v2/devices/{device_id}               # Get device
POST   /v2/terminals/actions                 # Create terminal action
GET    /v2/terminals/actions/{action_id}     # Get action
POST   /v2/terminals/actions/search          # Search actions
```

#### DarkVelocity Device API (Active)
```
POST   /api/device/code                       # Request device code (RFC 8628)
POST   /api/device/token                      # Poll for token
POST   /api/device/authorize                  # Authorize device
POST   /api/device/deny                       # Deny device
GET    /api/devices/{orgId}/{deviceId}        # Get device info
POST   /api/devices/{orgId}/{deviceId}/heartbeat   # Send heartbeat
POST   /api/devices/{orgId}/{deviceId}/suspend     # Suspend device
POST   /api/devices/{orgId}/{deviceId}/revoke      # Revoke device
```

**Comparison:**
| Feature | Square | DarkVelocity |
|---------|--------|--------------|
| Device pairing | ✅ Device codes | ✅ RFC 8628 Device Flow |
| Device management | ✅ | ✅ |
| Terminal actions | ✅ (checkout, refund) | ❌ |
| Heartbeat | ❌ | ✅ |
| Device suspend/revoke | ❌ | ✅ |

**Advantage:** DarkVelocity has richer device lifecycle management.

---

## Authentication Comparison

### Square OAuth 2.0
```
# Authorization URL
GET https://connect.squareup.com/oauth2/authorize
    ?client_id=CLIENT_ID
    &scope=PAYMENTS_WRITE+ORDERS_READ
    &session=false
    &state=STATE

# Token exchange
POST /oauth2/token
{
  "client_id": "CLIENT_ID",
  "client_secret": "CLIENT_SECRET",
  "code": "AUTHORIZATION_CODE",
  "grant_type": "authorization_code"
}
```

### DarkVelocity Authentication

**1. OAuth (Browser-based)**
```
GET  /api/oauth/login/{provider}    # Initiate OAuth flow
GET  /api/oauth/callback            # OAuth callback
GET  /api/oauth/userinfo            # Get user info (authenticated)
```

**2. Device Authorization (RFC 8628)**
```
POST /api/device/code               # Get device + user code
POST /api/device/token              # Poll for token
POST /api/device/authorize          # User authorizes
```

**3. PIN Authentication (Staff)**
```
POST /api/auth/pin                  # Login with PIN
POST /api/auth/logout               # Logout
POST /api/auth/refresh              # Refresh token
```

**Comparison:**
| Feature | Square | DarkVelocity |
|---------|--------|--------------|
| OAuth 2.0 | ✅ | ✅ |
| Device Flow | ❌ | ✅ (RFC 8628) |
| Staff PIN login | ❌ | ✅ |
| Token refresh | ✅ | ✅ |
| Scoped permissions | ✅ | ⚠️ Role-based |

**Advantage:** DarkVelocity has more flexible auth for POS scenarios (staff PIN, device flow).

---

## API Design Pattern Comparison

### URL Structure

**Square:**
```
/v2/{resource}                      # Collection
/v2/{resource}/{id}                 # Single resource
/v2/{resource}/{id}/{sub-resource}  # Nested resources
```

**DarkVelocity:**
```
/api/{resource}/{orgId}/{entityId}              # Org-scoped resource
/api/{resource}/{orgId}/{siteId}/{entityId}     # Site-scoped resource
```

### Request/Response Format

**Square:** Standard JSON with idempotency keys
```json
{
  "idempotency_key": "unique-key-12345",
  "order": { ... }
}
```

**DarkVelocity:** HAL+JSON with hypermedia links
```json
{
  "_links": {
    "self": { "href": "/api/orders/org1/order1" },
    "items": { "href": "/api/orders/org1/order1/items" }
  },
  "id": "order1",
  "status": "open"
}
```

### Error Handling

**Square:**
```json
{
  "errors": [
    {
      "category": "INVALID_REQUEST_ERROR",
      "code": "MISSING_REQUIRED_PARAMETER",
      "detail": "Missing required parameter: location_id",
      "field": "location_id"
    }
  ]
}
```

**DarkVelocity:**
```json
{
  "error": "invalid_request",
  "error_description": "Missing required parameter: location_id"
}
```

---

## Pricing Model Comparison

### Square
- **Payments:** 2.6% + $0.10 per tap/dip/swipe; 2.9% + $0.30 online
- **Orders API (non-Square payments):** 1% per transaction
- **APIs:** Free to use

### DarkVelocity
- Self-hosted, no per-transaction fees
- Customer provides own payment gateway
- Full control over costs

---

## Recommendations

### High Priority Gaps to Address

1. **Enable Core APIs** - Orders, Payments, Catalog, Inventory APIs are disabled
2. **Order Search** - Add order search/filter endpoint
3. **Batch Operations** - Add batch create/update for catalog and inventory
4. **Order Preview** - Add calculate endpoint for order totals
5. **Item Variations** - Support variations as first-class concept

### Medium Priority

6. **Fulfillments** - Add delivery/shipment fulfillment types
7. **Tips & Service Charges** - Add support in orders and payments
8. **Customer Groups** - Add customer segmentation
9. **Inventory Transfers** - Support multi-site inventory movement
10. **Payment Search** - Add payment history search

### Consider Adopting from Square

- **Idempotency Keys** - Make mandatory for mutations
- **Batch Endpoints** - Reduce API calls for bulk operations
- **Consistent Error Format** - Structured error categories and codes
- **Calculated Fields** - Return totals in responses automatically

### DarkVelocity Advantages to Preserve

- **Device Flow Auth** - Superior device onboarding (RFC 8628)
- **Staff PIN Auth** - Essential for POS quick access
- **Event Sourcing** - Full audit trail via Orleans
- **Multi-Tenant Design** - Organization/Site hierarchy
- **HAL+JSON** - Discoverable, self-documenting API
- **No Transaction Fees** - Self-hosted model

---

## Grain Interface vs Square API Comparison

This section compares DarkVelocity's Orleans grain interfaces (the actual domain implementation) against Square's API functionality.

### Summary: Grain Coverage vs Square

| Domain | Square API | DarkVelocity Grain | Grain Status | Notes |
|--------|------------|-------------------|--------------|-------|
| **Locations** | Locations API | `IOrganizationGrain`, `ISiteGrain` | ✅ Defined | Richer hierarchy (Org→Site) |
| **Catalog** | Catalog API | `IMenuItemGrain`, `IMenuCategoryGrain`, `IMenuDefinitionGrain` | ✅ Defined | More POS-specific (screens, buttons) |
| **Orders** | Orders API | `IOrderGrain` | ✅ Defined | Full feature parity |
| **Payments** | Payments API | `IPaymentGrain`, `ICashDrawerGrain` | ✅ Defined | Includes cash drawer management |
| **Inventory** | Inventory API | `IInventoryGrain` | ✅ Defined | **Superior:** FIFO, batches, expiry |
| **Customers** | Customers API | `ICustomerGrain` | ✅ Defined | Integrated loyalty |
| **Loyalty** | Loyalty API | `ILoyaltyProgramGrain` | ✅ Defined | **Superior:** Full program mgmt |
| **Gift Cards** | Gift Cards API | `IGiftCardGrain` | ✅ Defined | Full feature parity |
| **Team** | Team API | `IEmployeeGrain` | ✅ Defined | Basic employee management |
| **Labor** | Labor API | `IScheduleGrain`, `ITimeEntryGrain`, `ITipPoolGrain`, `IPayrollPeriodGrain` | ✅ Defined | **Superior:** Full labor suite |
| **Terminal** | Terminal API | `IDeviceGrain`, `IDeviceAuthGrain` | ✅ Active | **Superior:** RFC 8628 device flow |
| **Kitchen** | ❌ None | `IKitchenTicketGrain`, `IKitchenStationGrain` | ✅ Defined | **DV Exclusive** |
| **Bookings** | Bookings API | `IBookingGrain`, `IWaitlistGrain` | ✅ Defined | Full feature parity |
| **Reporting** | Reporting API | `IDailySalesGrain`, `IDailyInventorySnapshotGrain`, etc. | ✅ Defined | **Superior:** Real-time aggregation |
| **Webhooks** | Webhooks API | `IWebhookEndpointGrain` | ✅ Defined | Full feature parity |

---

### 1. Orders: `IOrderGrain` vs Square Orders API

**IOrderGrain Methods:**
```csharp
// Creation & State
Task<OrderCreatedResult> CreateAsync(CreateOrderCommand command);
Task<OrderState> GetStateAsync();

// Line Management
Task<AddLineResult> AddLineAsync(AddLineCommand command);
Task UpdateLineAsync(UpdateLineCommand command);
Task VoidLineAsync(VoidLineCommand command);
Task RemoveLineAsync(Guid lineId);

// Order Operations
Task SendAsync(Guid sentBy);
Task<OrderTotals> RecalculateTotalsAsync();
Task ApplyDiscountAsync(ApplyDiscountCommand command);
Task RemoveDiscountAsync(Guid discountId);
Task AddServiceChargeAsync(string name, decimal rate, bool isTaxable);

// Customer & Assignment
Task AssignCustomerAsync(Guid customerId, string? customerName);
Task AssignServerAsync(Guid serverId, string serverName);
Task TransferTableAsync(Guid newTableId, string newTableNumber, Guid transferredBy);

// Payment
Task RecordPaymentAsync(Guid paymentId, decimal amount, decimal tipAmount, string method);
Task RemovePaymentAsync(Guid paymentId);

// Completion
Task CloseAsync(Guid closedBy);
Task VoidAsync(VoidOrderCommand command);
Task ReopenAsync(Guid reopenedBy, string reason);
```

| Feature | Square Orders API | IOrderGrain | Status |
|---------|------------------|-------------|--------|
| Create order | `POST /v2/orders` | `CreateAsync()` | ✅ Parity |
| Get order | `GET /v2/orders/{id}` | `GetStateAsync()` | ✅ Parity |
| Add line items | Included in create/update | `AddLineAsync()` | ✅ Parity |
| Update line items | `PUT /v2/orders/{id}` | `UpdateLineAsync()` | ✅ Parity |
| Void line items | ❌ Delete only | `VoidLineAsync()` | ✅ **DV Better** |
| Apply discounts | Included in order | `ApplyDiscountAsync()` | ✅ Parity |
| Service charges | Included in order | `AddServiceChargeAsync()` | ✅ Parity |
| Calculate totals | `POST /v2/orders/calculate` | `RecalculateTotalsAsync()` | ✅ Parity |
| Clone order | `POST /v2/orders/clone` | `CloneAsync()` | ✅ Implemented |
| Search orders | `POST /v2/orders/search` | `IOrderBatchGrain.SearchAsync()` | ✅ Implemented |
| Batch retrieve | `POST /v2/orders/batch-retrieve` | `IOrderBatchGrain.BatchRetrieveAsync()` | ✅ Implemented |
| Assign server | ❌ Not native | `AssignServerAsync()` | ✅ **DV Exclusive** |
| Transfer table | ❌ Not native | `TransferTableAsync()` | ✅ **DV Exclusive** |
| Reopen order | ❌ Not supported | `ReopenAsync()` | ✅ **DV Exclusive** |
| Tips in payment | `tip_money` field | `RecordPaymentAsync(tipAmount)` | ✅ Parity |

**Gaps Addressed:**
- ✅ Added `CloneAsync()` method to `IOrderGrain`
- ✅ Implemented `IOrderBatchGrain` with search, batch retrieve, and calculate

---

### 2. Menu/Catalog: `IMenuItemGrain` vs Square Catalog API

**IMenuItemGrain Methods:**
```csharp
Task<MenuItemSnapshot> CreateAsync(CreateMenuItemCommand command);
Task<MenuItemSnapshot> UpdateAsync(UpdateMenuItemCommand command);
Task DeactivateAsync();
Task<MenuItemSnapshot> GetSnapshotAsync();
Task<decimal> GetPriceAsync();
Task AddModifierAsync(MenuItemModifier modifier);
Task RemoveModifierAsync(Guid modifierId);
Task UpdateCostAsync(decimal theoreticalCost);
```

**IMenuCategoryGrain Methods:**
```csharp
Task<MenuCategorySnapshot> CreateAsync(CreateMenuCategoryCommand command);
Task<MenuCategorySnapshot> UpdateAsync(UpdateMenuCategoryCommand command);
Task DeactivateAsync();
Task<MenuCategorySnapshot> GetSnapshotAsync();
```

**IMenuDefinitionGrain (POS Screen Layout):**
```csharp
Task<MenuDefinitionSnapshot> CreateAsync(CreateMenuDefinitionCommand command);
Task AddScreenAsync(MenuScreenDefinition screen);
Task AddButtonAsync(Guid screenId, MenuButtonDefinition button);
Task SetAsDefaultAsync();
```

| Feature | Square Catalog API | DV Menu Grains | Status |
|---------|-------------------|----------------|--------|
| Items | `ITEM` type | `IMenuItemGrain` | ✅ Parity |
| Categories | `CATEGORY` type | `IMenuCategoryGrain` | ✅ Parity |
| Modifiers | `MODIFIER_LIST`, `MODIFIER` | `MenuItemModifier` | ✅ Parity |
| Min/max modifiers | `min_selected_modifiers`, `max_selected_modifiers` | `MinSelections`, `MaxSelections` | ✅ Parity |
| Item variations | `ITEM_VARIATION` (first-class) | `IMenuItemVariationGrain` | ✅ Implemented |
| Images | `IMAGE` type | `ImageUrl` field only | ⚠️ Gap |
| SKU | `sku` field | `Sku` field | ✅ Parity |
| Batch operations | `batch-upsert`, `batch-retrieve` | `IMenuBatchGrain` | ✅ Implemented |
| Search | `/v2/catalog/search` | `IMenuBatchGrain.SearchAsync()` | ✅ Implemented |
| POS screen layout | ❌ Not supported | `IMenuDefinitionGrain` | ✅ **DV Exclusive** |
| Button configuration | ❌ Not supported | `MenuButtonDefinition` | ✅ **DV Exclusive** |
| Theoretical cost | ❌ Not supported | `UpdateCostAsync()` | ✅ **DV Exclusive** |
| Recipe link | ❌ Not supported | `RecipeId` field | ✅ **DV Exclusive** |

**Gaps Addressed:**
- ✅ Added `IMenuItemVariationGrain` for first-class variations
- ✅ Added `IMenuBatchGrain` with batch upsert, retrieve, delete, and search
- Remaining gap: Image management (only supports URL field, not dedicated image type)

---

### 3. Payments: `IPaymentGrain` vs Square Payments API

**IPaymentGrain Methods:**
```csharp
// Initiation
Task<PaymentInitiatedResult> InitiateAsync(InitiatePaymentCommand command);
Task<PaymentState> GetStateAsync();

// Completion by method
Task<PaymentCompletedResult> CompleteCashAsync(CompleteCashPaymentCommand command);
Task<PaymentCompletedResult> CompleteCardAsync(ProcessCardPaymentCommand command);
Task<PaymentCompletedResult> CompleteGiftCardAsync(ProcessGiftCardPaymentCommand command);

// Card authorization flow
Task RequestAuthorizationAsync();
Task RecordAuthorizationAsync(string authCode, string gatewayRef, CardInfo cardInfo);
Task RecordDeclineAsync(string declineCode, string reason);
Task CaptureAsync();

// Modifications
Task<RefundResult> RefundAsync(RefundPaymentCommand command);
Task<RefundResult> PartialRefundAsync(RefundPaymentCommand command);
Task VoidAsync(VoidPaymentCommand command);
Task AdjustTipAsync(AdjustTipCommand command);

// Batch management
Task AssignToBatchAsync(Guid batchId);
```

**ICashDrawerGrain Methods:**
```csharp
Task<DrawerOpenedResult> OpenAsync(OpenDrawerCommand command);
Task RecordCashInAsync(RecordCashInCommand command);
Task RecordCashOutAsync(RecordCashOutCommand command);
Task RecordDropAsync(CashDropCommand command);
Task OpenNoSaleAsync(Guid userId, string? reason);
Task CountAsync(CountDrawerCommand command);
Task<DrawerClosedResult> CloseAsync(CloseDrawerCommand command);
```

| Feature | Square Payments API | DV Payment Grains | Status |
|---------|--------------------|--------------------|--------|
| Create payment | `POST /v2/payments` | `InitiateAsync()` | ✅ Parity |
| Cash payments | Supported | `CompleteCashAsync()` | ✅ Parity |
| Card payments | Native processing | `CompleteCardAsync()` | ✅ Via gateway |
| Gift card payments | Supported | `CompleteGiftCardAsync()` | ✅ Parity |
| Delayed capture | `autocomplete: false` | `RequestAuthorizationAsync()` → `CaptureAsync()` | ✅ Parity |
| Refunds (full) | `POST /v2/refunds` | `RefundAsync()` | ✅ Parity |
| Refunds (partial) | Amount parameter | `PartialRefundAsync()` | ✅ Parity |
| Void payment | `POST /v2/payments/{id}/cancel` | `VoidAsync()` | ✅ Parity |
| Tip adjustment | `PUT /v2/payments/{id}` | `AdjustTipAsync()` | ✅ Parity |
| Batch settlement | Automatic | `AssignToBatchAsync()` | ✅ **DV Better** (explicit) |
| Cash drawer | ❌ Not supported | `ICashDrawerGrain` | ✅ **DV Exclusive** |
| Cash drops | ❌ Not supported | `RecordDropAsync()` | ✅ **DV Exclusive** |
| Drawer count | ❌ Not supported | `CountAsync()` | ✅ **DV Exclusive** |
| No-sale open | ❌ Not supported | `OpenNoSaleAsync()` | ✅ **DV Exclusive** |
| Search payments | `GET /v2/payments` | `IPaymentBatchGrain.SearchAsync()` | ✅ Implemented |

**Gaps Addressed:**
- ✅ Added `IPaymentBatchGrain` with search, date range queries, and batch retrieval

---

### 4. Inventory: `IInventoryGrain` vs Square Inventory API

**IInventoryGrain Methods:**
```csharp
// Initialization
Task InitializeAsync(InitializeInventoryCommand command);

// Receiving
Task<BatchReceivedResult> ReceiveBatchAsync(ReceiveBatchCommand command);
Task<BatchReceivedResult> ReceiveTransferAsync(ReceiveTransferCommand command);

// Consumption (FIFO)
Task<ConsumptionResult> ConsumeAsync(ConsumeStockCommand command);
Task<ConsumptionResult> ConsumeForOrderAsync(Guid orderId, decimal quantity, Guid? performedBy);
Task ReverseConsumptionAsync(Guid movementId, string reason, Guid reversedBy);

// Waste & Adjustments
Task RecordWasteAsync(RecordWasteCommand command);
Task AdjustQuantityAsync(AdjustQuantityCommand command);
Task RecordPhysicalCountAsync(decimal countedQuantity, Guid countedBy, Guid? approvedBy);

// Transfers
Task TransferOutAsync(TransferOutCommand command);

// Batch management
Task WriteOffExpiredBatchesAsync(Guid performedBy);

// Configuration
Task SetReorderPointAsync(decimal reorderPoint);
Task SetParLevelAsync(decimal parLevel);

// Queries
Task<InventoryLevelInfo> GetLevelInfoAsync();
Task<bool> HasSufficientStockAsync(decimal quantity);
Task<StockLevel> GetStockLevelAsync();
Task<IReadOnlyList<StockBatch>> GetActiveBatchesAsync();
```

| Feature | Square Inventory API | IInventoryGrain | Status |
|---------|---------------------|-----------------|--------|
| Get inventory count | `GET /v2/inventory/{id}` | `GetLevelInfoAsync()` | ✅ Parity |
| Adjust inventory | `POST /v2/inventory/batch-change` | `AdjustQuantityAsync()` | ✅ Parity |
| Physical count | `POST /v2/inventory/physical-count` | `RecordPhysicalCountAsync()` | ✅ Parity |
| Transfer between locations | `POST /v2/inventory/transfer` | `TransferOutAsync()` + `ReceiveTransferAsync()` | ✅ Parity |
| Change history | `POST /v2/inventory/batch-retrieve-changes` | Via events | ✅ Parity |
| Batch operations | Native | `IInventoryBatchGrain` | ✅ Implemented |
| **FIFO costing** | ❌ Not supported | Native | ✅ **DV Exclusive** |
| **Batch tracking** | ❌ Not supported | `ReceiveBatchAsync()`, `GetActiveBatchesAsync()` | ✅ **DV Exclusive** |
| **Expiry tracking** | ❌ Not supported | `ExpiryDate`, `WriteOffExpiredBatchesAsync()` | ✅ **DV Exclusive** |
| **Weighted avg cost** | ❌ Not supported | `WeightedAverageCost` | ✅ **DV Exclusive** |
| **Waste recording** | `WASTE` state only | `RecordWasteAsync()` with categories | ✅ **DV Better** |
| **Par levels** | ❌ Not supported | `SetParLevelAsync()` | ✅ **DV Exclusive** |
| **Reorder points** | ❌ Not supported | `SetReorderPointAsync()` | ✅ **DV Exclusive** |
| **Consumption reversal** | ❌ Not supported | `ReverseConsumptionAsync()` | ✅ **DV Exclusive** |
| **Stock level alerts** | ❌ Not supported | `StockLevel` enum | ✅ **DV Exclusive** |

**DarkVelocity Inventory is Superior** - Full food-service inventory with FIFO costing, batch tracking, expiry management, and waste categorization.

---

### 5. Customers & Loyalty: `ICustomerGrain` + `ILoyaltyProgramGrain` vs Square

**ICustomerGrain Methods:**
```csharp
// CRUD
Task<CustomerCreatedResult> CreateAsync(CreateCustomerCommand command);
Task UpdateAsync(UpdateCustomerCommand command);

// Tags & Notes
Task AddTagAsync(string tag);
Task AddNoteAsync(string content, Guid createdBy);

// Loyalty (Integrated)
Task EnrollInLoyaltyAsync(EnrollLoyaltyCommand command);
Task<PointsResult> EarnPointsAsync(EarnPointsCommand command);
Task<PointsResult> RedeemPointsAsync(RedeemPointsCommand command);
Task PromoteTierAsync(Guid newTierId, string tierName, int pointsToNextTier);

// Rewards
Task<RewardResult> IssueRewardAsync(IssueRewardCommand command);
Task RedeemRewardAsync(RedeemRewardCommand command);

// Visits
Task RecordVisitAsync(RecordVisitCommand command);

// Referrals
Task SetReferralCodeAsync(string code);
Task IncrementReferralCountAsync();

// GDPR
Task DeleteAsync();
Task AnonymizeAsync();
```

**ILoyaltyProgramGrain Methods:**
```csharp
// Program management
Task<LoyaltyProgramCreatedResult> CreateAsync(CreateLoyaltyProgramCommand command);
Task ActivateAsync();
Task PauseAsync();
Task DeactivateAsync();

// Earning rules
Task<EarningRuleResult> AddEarningRuleAsync(AddEarningRuleCommand command);
Task UpdateEarningRuleAsync(Guid ruleId, bool isActive);

// Tiers
Task<TierResult> AddTierAsync(AddTierCommand command);
Task<LoyaltyTier?> GetNextTierAsync(int currentLevel);

// Rewards
Task<RewardDefinitionResult> AddRewardAsync(AddRewardCommand command);
Task<IReadOnlyList<RewardDefinition>> GetAvailableRewardsAsync(int tierLevel);

// Points calculation
Task<PointsCalculation> CalculatePointsAsync(decimal spendAmount, int customerTierLevel, ...);

// Configuration
Task ConfigurePointsExpiryAsync(ConfigurePointsExpiryCommand command);
Task ConfigureReferralProgramAsync(ConfigureReferralCommand command);
```

| Feature | Square Loyalty API | DV Customer/Loyalty Grains | Status |
|---------|-------------------|---------------------------|--------|
| Create customer | `POST /v2/customers` | `CreateAsync()` | ✅ Parity |
| Search customers | `POST /v2/customers/search` | `ICustomerBatchGrain.SearchAsync()` | ✅ Implemented |
| Customer groups | `POST /v2/customers/groups` | `ICustomerBatchGrain.CreateGroupAsync()` | ✅ Implemented |
| **Loyalty program design** | ❌ UI only | `ILoyaltyProgramGrain` | ✅ **DV Exclusive** |
| **Custom earning rules** | Limited | `AddEarningRuleAsync()` | ✅ **DV Better** |
| **Tier system** | Basic | Full with benefits, multipliers | ✅ **DV Better** |
| **Points expiry config** | ❌ Not configurable | `ConfigurePointsExpiryAsync()` | ✅ **DV Exclusive** |
| **Referral program** | ❌ Not supported | `ConfigureReferralProgramAsync()` | ✅ **DV Exclusive** |
| **Visit tracking** | ❌ Not supported | `RecordVisitAsync()` | ✅ **DV Exclusive** |
| **GDPR compliance** | Manual | `DeleteAsync()`, `AnonymizeAsync()` | ✅ **DV Better** |
| Earn points | `POST /v2/loyalty/accounts/{id}/accumulate` | `EarnPointsAsync()` | ✅ Parity |
| Redeem rewards | `POST /v2/loyalty/rewards/{id}/redeem` | `RedeemRewardAsync()` | ✅ Parity |

**DarkVelocity Loyalty is Superior** - Full loyalty program management including tier design, custom earning rules, referral programs, and GDPR compliance.

---

### 6. Labor: DarkVelocity Grains vs Square Team/Labor APIs

**Square has Team API and Labor API. DarkVelocity has a comprehensive labor management suite:**

| Grain | Purpose | Square Equivalent |
|-------|---------|-------------------|
| `IEmployeeGrain` | Employee profile, clock in/out | Team Members API |
| `IRoleGrain` | Job roles with rates | Team Member Wages |
| `IScheduleGrain` | Weekly scheduling | Shifts API |
| `ITimeEntryGrain` | Time tracking | Labor API |
| `ITipPoolGrain` | Tip distribution | ❌ None |
| `IPayrollPeriodGrain` | Payroll calculation | ❌ None |
| `IEmployeeAvailabilityGrain` | Availability management | ❌ None |
| `IShiftSwapGrain` | Shift swap requests | ❌ None |
| `ITimeOffGrain` | Time off requests | ❌ None |

| Feature | Square Labor API | DV Labor Grains | Status |
|---------|-----------------|-----------------|--------|
| Employee profiles | Team Members API | `IEmployeeGrain` | ✅ Parity |
| Clock in/out | Break API | `ClockInAsync()`, `ClockOutAsync()` | ✅ Parity |
| Scheduling | Shifts API | `IScheduleGrain` | ✅ Parity |
| Time tracking | Labor API | `ITimeEntryGrain` | ✅ Parity |
| **Tip pooling** | ❌ Not supported | `ITipPoolGrain` | ✅ **DV Exclusive** |
| **Payroll calculation** | ❌ Not supported | `IPayrollPeriodGrain` | ✅ **DV Exclusive** |
| **Availability management** | ❌ Not supported | `IEmployeeAvailabilityGrain` | ✅ **DV Exclusive** |
| **Shift swaps** | ❌ Not supported | `IShiftSwapGrain` | ✅ **DV Exclusive** |
| **Time off requests** | ❌ Not supported | `ITimeOffGrain` | ✅ **DV Exclusive** |
| **Overtime calculation** | Manual | Automatic with rates | ✅ **DV Better** |
| **Multiple clock methods** | Basic | PIN, QR, Biometric, Manager | ✅ **DV Better** |

**DarkVelocity Labor is Superior** - Complete labor management including tip pools, payroll, availability, shift swaps, and time off tracking.

---

### 7. Kitchen Display: DarkVelocity Exclusive

**Square has no Kitchen Display System APIs.** DarkVelocity provides:

**IKitchenTicketGrain:**
```csharp
Task<KitchenTicketCreatedResult> CreateAsync(CreateKitchenTicketCommand command);
Task AddItemAsync(AddTicketItemCommand command);
Task StartItemAsync(StartItemCommand command);
Task CompleteItemAsync(CompleteItemCommand command);
Task VoidItemAsync(VoidItemCommand command);
Task ReceiveAsync();
Task StartAsync();
Task BumpAsync(Guid bumpedBy);
Task MarkRushAsync();
Task MarkVipAsync();
Task FireAllAsync();
```

**IKitchenStationGrain:**
```csharp
Task OpenAsync(OpenStationCommand command);
Task AssignItemsAsync(AssignItemsToStationCommand command);
Task SetPrinterAsync(Guid printerId);
Task SetDisplayAsync(Guid displayId);
Task ReceiveTicketAsync(Guid ticketId);
Task CompleteTicketAsync(Guid ticketId);
Task PauseAsync();
Task ResumeAsync();
```

| Feature | Square | DarkVelocity KDS | Status |
|---------|--------|------------------|--------|
| Kitchen tickets | ❌ None | `IKitchenTicketGrain` | ✅ **DV Exclusive** |
| Station management | ❌ None | `IKitchenStationGrain` | ✅ **DV Exclusive** |
| Item-level tracking | ❌ None | `StartItemAsync()`, `CompleteItemAsync()` | ✅ **DV Exclusive** |
| Rush/VIP orders | ❌ None | `MarkRushAsync()`, `MarkVipAsync()` | ✅ **DV Exclusive** |
| Course firing | ❌ None | `FireAllAsync()` | ✅ **DV Exclusive** |
| Station routing | ❌ None | `AssignItemsAsync()` | ✅ **DV Exclusive** |
| Prep time tracking | ❌ None | `GetTimingsAsync()` | ✅ **DV Exclusive** |

---

### 8. Reporting: DarkVelocity Aggregation Grains

**Square has basic reporting via the Transactions API. DarkVelocity has real-time aggregation grains:**

| Grain | Purpose | Square Equivalent |
|-------|---------|-------------------|
| `IDailySalesGrain` | Daily sales aggregation | Transactions API (limited) |
| `IDailyInventorySnapshotGrain` | Daily inventory snapshot | ❌ None |
| `IDailyConsumptionGrain` | Consumption tracking | ❌ None |
| `IDailyWasteGrain` | Waste tracking | ❌ None |
| `IPeriodAggregationGrain` | Weekly/monthly rollup | ❌ None |
| `ISiteDashboardGrain` | Real-time dashboard | ❌ None |

| Feature | Square | DarkVelocity Reporting | Status |
|---------|--------|----------------------|--------|
| Sales totals | Basic | `IDailySalesGrain` with breakdown | ✅ **DV Better** |
| Inventory valuation | ❌ None | `IDailyInventorySnapshotGrain` | ✅ **DV Exclusive** |
| **Consumption variance** | ❌ None | `IDailyConsumptionGrain` | ✅ **DV Exclusive** |
| **Waste tracking** | ❌ None | `IDailyWasteGrain` | ✅ **DV Exclusive** |
| **Gross profit (FIFO vs WAC)** | ❌ None | `GetGrossProfitMetricsAsync()` | ✅ **DV Exclusive** |
| **Period rollups** | Manual | `IPeriodAggregationGrain` | ✅ **DV Exclusive** |
| **Real-time dashboard** | ❌ None | `ISiteDashboardGrain` | ✅ **DV Exclusive** |

---

### 9. Gift Cards: `IGiftCardGrain` vs Square Gift Cards API

| Feature | Square Gift Cards API | IGiftCardGrain | Status |
|---------|----------------------|----------------|--------|
| Create gift card | `POST /v2/gift-cards` | `CreateAsync()` | ✅ Parity |
| Activate | `POST /v2/gift-cards/{id}/activate` | `ActivateAsync()` | ✅ Parity |
| Redeem | `POST /v2/gift-cards/activities` | `RedeemAsync()` | ✅ Parity |
| Reload | `POST /v2/gift-cards/activities` | `ReloadAsync()` | ✅ Parity |
| Balance check | `GET /v2/gift-cards/{id}` | `GetBalanceInfoAsync()` | ✅ Parity |
| Transaction history | `POST /v2/gift-cards/activities` | `GetTransactionsAsync()` | ✅ Parity |
| **PIN validation** | ❌ None | `ValidatePinAsync()` | ✅ **DV Exclusive** |
| **Recipient info** | ❌ None | `SetRecipientAsync()` | ✅ **DV Exclusive** |
| **Void transaction** | ❌ None | `VoidTransactionAsync()` | ✅ **DV Exclusive** |

---

### 10. Bookings: `IBookingGrain` vs Square Bookings API

| Feature | Square Bookings API | IBookingGrain | Status |
|---------|--------------------|--------------||--------|
| Create booking | `POST /v2/bookings` | `RequestAsync()` | ✅ Parity |
| Confirm | Update status | `ConfirmAsync()` | ✅ Parity |
| Cancel | `POST /v2/bookings/{id}/cancel` | `CancelAsync()` | ✅ Parity |
| **Waitlist** | ❌ None | `IWaitlistGrain` | ✅ **DV Exclusive** |
| **Deposit management** | ❌ None | `RequireDepositAsync()`, `RecordDepositPaymentAsync()` | ✅ **DV Exclusive** |
| **Table assignment** | ❌ None | `AssignTableAsync()` | ✅ **DV Exclusive** |
| **Arrival tracking** | ❌ None | `RecordArrivalAsync()` | ✅ **DV Exclusive** |
| **No-show tracking** | ❌ None | `MarkNoShowAsync()` | ✅ **DV Exclusive** |

---

## Overall Assessment

### DarkVelocity Advantages (Grains > Square)

1. **Inventory Management** - FIFO costing, batch tracking, expiry, waste categorization
2. **Loyalty Program Design** - Full program management, custom rules, tier design
3. **Labor Management** - Tip pools, payroll, availability, shift swaps, time off
4. **Kitchen Display System** - Complete KDS with stations, routing, timing
5. **Reporting** - Real-time aggregation, consumption variance, gross profit analysis
6. **Device Authentication** - RFC 8628 device flow, PIN auth, device lifecycle
7. **Cash Drawer Management** - Full drawer operations, counts, drops
8. **Booking/Waitlist** - Deposits, table assignment, arrival tracking

### Square Advantages (Square > Grains)

1. ~~**Batch Operations**~~ - ✅ Now implemented via `IMenuBatchGrain`, `IInventoryBatchGrain`, `IOrderBatchGrain`, `ICustomerBatchGrain`, `IPaymentBatchGrain`
2. ~~**Search APIs**~~ - ✅ Now implemented via batch grains with `SearchAsync()` methods
3. ~~**Item Variations**~~ - ✅ Now implemented via `IMenuItemVariationGrain`
4. **Payment Processing** - Square provides native payment processing (DV uses external gateway)
5. **Hosted Checkout** - Square offers hosted checkout pages

### Feature Parity

- Orders, Payments, Customers, Gift Cards, Bookings - Core functionality aligned
- Discounts, Service Charges, Tips - Both support these concepts
- Multi-location - Both support multi-site/location structures

---

## Sources

- [Square API Reference](https://developer.squareup.com/reference/square)
- [Square Orders API](https://developer.squareup.com/docs/orders-api/what-it-does)
- [Square Catalog API](https://developer.squareup.com/docs/catalog-api/what-it-does)
- [Square Payments API](https://developer.squareup.com/reference/square/payments-api)
- [Square Inventory API](https://developer.squareup.com/reference/square/inventory-api)
- [Square Locations API](https://developer.squareup.com/docs/locations-api)
- [Square Checkout API](https://developer.squareup.com/docs/checkout-api)
