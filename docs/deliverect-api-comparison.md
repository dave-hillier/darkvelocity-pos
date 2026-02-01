# Deliverect API vs DarkVelocity POS API Comparison

This document compares the Deliverect integration APIs with the current DarkVelocity POS API structure to identify gaps, alignment opportunities, and integration requirements.

## Executive Summary

| Aspect | Deliverect | DarkVelocity POS |
|--------|------------|------------------|
| **Architecture** | REST API with webhooks | Orleans Grains + Minimal APIs |
| **Auth** | OAuth2 (client_credentials) + HMAC | OAuth2 + Device Flow + JWT |
| **Response Format** | Plain JSON | HAL+JSON |
| **Order Flow** | Push-based (webhooks) | Event-sourced (Orleans streams) |
| **Multi-tenancy** | Account → Location | Organization → Site |

---

## 1. Authentication Comparison

### Deliverect Authentication
| Method | Description |
|--------|-------------|
| **OAuth2 M2M** | `client_id` + `client_secret` for machine-to-machine |
| **HMAC** | Request signing for webhook verification |
| **IP Whitelisting** | Additional security layer |
| **Scopes** | `genericPOS`, `store`, `genericKDS`, etc. |

### DarkVelocity Authentication
| Method | Description |
|--------|-------------|
| **OAuth2** | Google/Microsoft social login |
| **Device Flow** | RFC 8628 for headless devices (POS terminals) |
| **PIN Auth** | Staff login on authenticated devices |
| **JWT** | Access + Refresh tokens with claims |

### Gap Analysis
| Gap | Impact | Recommendation |
|-----|--------|----------------|
| No M2M auth in DarkVelocity | Cannot integrate as Deliverect POS partner | Add `client_credentials` grant |
| No HMAC webhook verification | Cannot verify Deliverect webhook authenticity | Implement HMAC signature validation |
| No API key scopes | Cannot restrict partner access | Add scope-based authorization |

---

## 2. Order Management Comparison

### Deliverect Order Model
```json
{
  "_id": "order_123",
  "channelOrderId": "UBER-456",
  "channelOrderDisplayId": "#1234",
  "status": 20,
  "orderType": 1,
  "channel": 7,
  "account": "acc_123",
  "location": "loc_456",
  "customer": {
    "name": "John Doe",
    "phoneNumber": "+1234567890",
    "email": "john@example.com"
  },
  "deliveryAddress": {
    "street": "123 Main St",
    "postalCode": "12345",
    "city": "Anytown",
    "country": "US"
  },
  "items": [{
    "plu": "BURGER-001",
    "name": "Classic Burger",
    "quantity": 2,
    "price": 1299,
    "subItems": [{
      "plu": "MOD-CHEESE",
      "name": "Extra Cheese",
      "quantity": 1,
      "price": 150
    }]
  }],
  "payment": {
    "amount": 2898,
    "type": 1,
    "due": 0
  },
  "deliveryCost": 499,
  "tip": 200,
  "pickupTime": "2026-02-01T12:30:00Z",
  "orderIsAlreadyPaid": true
}
```

### DarkVelocity External Order Model
```csharp
ExternalOrderState {
    ExternalOrderId: Guid,
    PlatformOrderId: string,
    PlatformOrderNumber: string,
    Status: ExternalOrderStatus,
    OrderType: ExternalOrderType,
    DeliveryPlatformId: Guid,
    LocationId: Guid,
    Customer: {
        Name: string,
        Phone: string,
        Email: string
    },
    Items: [{
        ExternalItemId: string,
        Name: string,
        Quantity: int,
        UnitPrice: decimal,
        Modifiers: [...]
    }],
    Subtotal: decimal,
    DeliveryFee: decimal,
    ServiceFee: decimal,
    Tax: decimal,
    Tip: decimal,
    Total: decimal,
    Currency: string
}
```

### Field Mapping

| Deliverect Field | DarkVelocity Field | Notes |
|-----------------|-------------------|-------|
| `_id` | `ExternalOrderId` | Need to store Deliverect ID |
| `channelOrderId` | `PlatformOrderId` | Direct mapping |
| `channelOrderDisplayId` | `PlatformOrderNumber` | Direct mapping |
| `status` (int) | `ExternalOrderStatus` (enum) | Need status mapping |
| `orderType` | `ExternalOrderType` | 1=Delivery, 2=Pickup, 3=DineIn |
| `channel` | `DeliveryPlatformId` | Need channel→platform mapping |
| `account` | - | Maps to Organization |
| `location` | `LocationId` | Need location mapping |
| `customer.*` | `Customer.*` | Direct mapping |
| `deliveryAddress` | - | **Missing in DarkVelocity** |
| `items[].plu` | `Items[].ExternalItemId` | Direct mapping |
| `items[].subItems` | `Items[].Modifiers` | Direct mapping |
| `payment.amount` | `Total` | Cents vs decimal |
| `deliveryCost` | `DeliveryFee` | Direct mapping |
| `tip` | `Tip` | Direct mapping |
| `pickupTime` | - | **Missing in DarkVelocity** |
| `deliveryTime` | - | **Missing in DarkVelocity** |
| `courier` | - | **Missing in DarkVelocity** |

### Missing in DarkVelocity
- Delivery address storage
- Scheduled pickup/delivery times
- Courier tracking information
- Channel-specific display IDs
- Discount breakdown by provider
- Packaging preferences (cutlery, bags)

---

## 3. Order Status Comparison

### Deliverect Status Codes
| Code | Meaning | Description |
|------|---------|-------------|
| 2 | Received | Order received from channel |
| 10 | Pending | Awaiting POS acceptance |
| 20 | Accepted | POS accepted the order |
| 30 | Preparing | Kitchen started preparation |
| 40 | Prepared | Ready for pickup/delivery |
| 50 | Picked Up | Courier collected |
| 60 | Delivered | Order delivered |
| 90 | Finalized | Order completed |
| 95 | Auto-finalized | System auto-closed |
| 110 | Cancelled | Order cancelled |
| 120 | Failed | Order failed |

### DarkVelocity External Order Status
```csharp
public enum ExternalOrderStatus {
    Received,
    Accepted,
    Rejected,
    Preparing,
    Ready,
    PickedUp,
    Delivered,
    Cancelled,
    Failed
}
```

### Status Mapping Required
| Deliverect | DarkVelocity | Action Needed |
|------------|--------------|---------------|
| 2, 10 | `Received` | Map both |
| 20 | `Accepted` | Direct |
| 30 | `Preparing` | Direct |
| 40 | `Ready` | Direct |
| 50 | `PickedUp` | Direct |
| 60 | `Delivered` | Direct |
| 90, 95 | - | Add `Finalized` status |
| 110 | `Cancelled` | Direct |
| 120 | `Failed` | Direct |

---

## 4. Product/Menu Sync Comparison

### Deliverect Product Sync Endpoint
```
POST /products/{accountId}
```

```json
{
  "locationId": "loc_123",
  "products": [{
    "productType": 1,
    "name": "Classic Burger",
    "plu": "BURGER-001",
    "price": 1299,
    "deliveryTax": 10,
    "takeawayTax": 10,
    "eatInTax": 20,
    "description": "Juicy beef burger",
    "imageUrl": "https://...",
    "max": 100,
    "subProducts": ["MOD-CHEESE", "MOD-BACON"],
    "productTags": [1, 5],
    "snoozed": false
  }],
  "categories": [{
    "name": "Burgers",
    "categoryId": "CAT-BURGERS"
  }]
}
```

### DarkVelocity Menu Sync
```csharp
IMenuSyncGrain {
    Task<MenuSyncSnapshot> StartAsync(StartMenuSyncCommand)
    Task RecordItemSyncedAsync(MenuItemMappingRecord)
    Task RecordItemFailedAsync(Guid menuItemId, string error)
    Task CompleteAsync()
    Task FailAsync(string error)
}
```

### Gap Analysis
| Feature | Deliverect | DarkVelocity | Gap |
|---------|------------|--------------|-----|
| Push menu to channel | Yes | Partial | Need outbound sync |
| Pull menu from channel | Yes | No | Need import capability |
| Product snooze/availability | Yes | Unknown | Check inventory integration |
| Multi-tax rates | Yes (delivery/takeaway/eatIn) | Unknown | May need tax context |
| Product tags/allergens | Yes | Unknown | Check menu metadata |
| Image URLs | Yes | Unknown | Check asset management |

---

## 5. Webhook Comparison

### Deliverect Webhooks (POS receives)
| Webhook | Trigger | DarkVelocity Equivalent |
|---------|---------|------------------------|
| Order Notification | New order from channel | `IExternalOrderGrain.CreateAsync` |
| Order Cancellation | Channel cancels order | `IExternalOrderGrain.CancelAsync` |
| Sync Products | Channel requests menu | Need to implement |
| Sync Tables | Channel requests tables | Not applicable |
| Tax Calculation | Dynamic tax request | Not implemented |

### Deliverect Webhooks (POS sends)
| Webhook | Trigger | DarkVelocity Equivalent |
|---------|---------|------------------------|
| Order Status Update | Status changes | Need HTTP callback |
| Register POS | Initial connection | Need registration flow |

### DarkVelocity Webhook System
```csharp
IWebhookEndpointState {
    EndpointId: Guid,
    MerchantId: Guid,
    Url: string,
    EnabledEvents: List<string>,  // ["*", "order.*"]
    Secret: string                 // HMAC signing
}
```

### Required Implementation
1. **Inbound webhook handler** for Deliverect order notifications
2. **Outbound webhook sender** for status updates to Deliverect
3. **HMAC validation** for Deliverect webhook security
4. **Menu sync responder** for channel product requests

---

## 6. API Endpoint Comparison

### Deliverect POS Endpoints
| Endpoint | Method | DarkVelocity Equivalent |
|----------|--------|------------------------|
| `/products/{accountId}` | POST | Menu sync grain (partial) |
| `/order-status` | POST | Need to implement |
| `/inventory` | POST | Inventory grain exists |
| `/accounts` | GET | Organization grain |
| `/locations` | GET | Site grain |
| `/channels` | GET | DeliveryPlatform grain |
| `/allergens-tags` | GET | Need to implement |

### DarkVelocity Unique Endpoints
| Endpoint | Purpose | Deliverect Equivalent |
|----------|---------|----------------------|
| `/api/oauth/*` | Social login | N/A |
| `/api/device/*` | Device auth | N/A (different use case) |
| `/api/auth/pin` | Staff PIN | N/A |
| `/api/stations/*` | KDS stations | KDS API scope |

---

## 7. Integration Architecture Recommendations

### Option A: Direct Deliverect Integration
```
┌─────────────┐      ┌──────────────┐      ┌─────────────────┐
│  Deliverect │ ───▶ │   Webhook    │ ───▶ │  ExternalOrder  │
│   Channel   │      │   Handler    │      │     Grain       │
└─────────────┘      └──────────────┘      └─────────────────┘
                            │
                            ▼
                     ┌──────────────┐
                     │   Outbound   │
                     │   Notifier   │
                     └──────────────┘
                            │
                            ▼
                     ┌─────────────┐
                     │  Deliverect │
                     │  Status API │
                     └─────────────┘
```

### Option B: Adapter Pattern
```
┌─────────────┐      ┌──────────────┐      ┌─────────────────┐
│  Deliverect │ ───▶ │  Deliverect  │ ───▶ │  DarkVelocity   │
│     API     │      │   Adapter    │      │   Core Grains   │
└─────────────┘      └──────────────┘      └─────────────────┘
```

### Recommended New Components

1. **DeliverectWebhookController**
   - Handle order notifications
   - Validate HMAC signatures
   - Map to internal models

2. **DeliverectApiClient**
   - Send status updates
   - Push menu updates
   - Handle authentication

3. **DeliverectMappingService**
   - Status code mapping
   - Product PLU mapping
   - Location/channel mapping

4. **Configuration**
   ```json
   {
     "Deliverect": {
       "BaseUrl": "https://api.deliverect.com",
       "StagingUrl": "https://api.staging.deliverect.com",
       "ClientId": "...",
       "ClientSecret": "...",
       "WebhookSecret": "...",
       "TimeoutSeconds": 30
     }
   }
   ```

---

## 8. Implementation Priority

### Phase 1: Core Integration (MVP)
1. Add Deliverect webhook endpoint for order reception
2. Implement order status callback to Deliverect
3. Add HMAC signature validation
4. Create status code mapping

### Phase 2: Menu Sync
1. Implement menu push to Deliverect
2. Add product availability (snooze) support
3. Handle menu pull requests

### Phase 3: Advanced Features
1. Courier tracking integration
2. Delivery address management
3. Real-time ETA updates
4. Multi-channel reporting

---

## 9. Data Model Changes Required

### ExternalOrderState Additions
```csharp
// New fields needed
public string? DeliverectOrderId { get; set; }
public string? ChannelDisplayId { get; set; }
public DeliveryAddress? DeliveryAddress { get; set; }
public DateTime? ScheduledPickupTime { get; set; }
public DateTime? ScheduledDeliveryTime { get; set; }
public bool IsAsapDelivery { get; set; }
public CourierInfo? Courier { get; set; }
public List<DiscountInfo>? Discounts { get; set; }
public PackagingPreferences? Packaging { get; set; }
public string? CustomerNote { get; set; }

public record DeliveryAddress(
    string Street,
    string PostalCode,
    string City,
    string Country,
    string? ExtraInfo
);

public record CourierInfo(
    string? FirstName,
    string? LastName,
    string? PhoneNumber,
    string? Provider,
    int? Status
);

public record DiscountInfo(
    string Type,
    string Provider,  // "restaurant" or "channel"
    string Name,
    decimal Amount
);
```

### New Enum Values
```csharp
public enum ExternalOrderStatus {
    // Existing
    Received, Accepted, Rejected, Preparing, Ready,
    PickedUp, Delivered, Cancelled, Failed,
    // New
    Pending,     // Awaiting acceptance
    Finalized,   // Successfully completed
    AutoFinalized // System auto-closed
}
```

---

## 10. References

- [Deliverect Developer Hub](https://developers.deliverect.com/)
- [Deliverect POS Endpoints](https://developers.deliverect.com/reference/pos_endpoints)
- [Order Notification Webhook](https://developers.deliverect.com/reference/post-orders-webhook)
- [Order Status Update](https://developers.deliverect.com/reference/post-order-status-update)
- [API Authentication](https://developers.deliverect.com/docs/getting-started)
- [Order Flow](https://developers.deliverect.com/docs/order-flow)
