# DarkVelocity POS

A full-featured Point of Sale system inspired by Lightspeed Restaurant K-Series, built with .NET 10 and React.

## Architecture

**Backend**: Service-Oriented Architecture with 10 discrete .NET 10 Web APIs

| Service | Purpose | Database |
|---------|---------|----------|
| Auth | User authentication, PIN/QR login, permissions | auth_db |
| Location | Multi-location management, settings, operating hours | location_db |
| Menu | Items, categories, menus, accounting groups | menu_db |
| Orders | Order lifecycle, sales periods | orders_db |
| Payments | Cash/card payments, Stripe integration | payments_db |
| Hardware | Printers, cash drawers, POS devices | hardware_db |
| Inventory | Ingredients, stock batches, FIFO tracking | inventory_db |
| Procurement | Suppliers, purchase orders, deliveries | procurement_db |
| Costing | Recipes, cost calculations, margin analysis | costing_db |
| Reporting | Sales reports, COGS, margin projections | reporting_db |

**Frontend**: React SPAs (planned)
- POS Application - Offline-first PWA for tablets
- Back Office - Configuration and reporting SPA

## Key Features

- **Multi-location support** with timezone and currency handling
- **Recipe-based costing** with waste percentage calculations
- **FIFO inventory tracking** for accurate COGS
- **Margin analysis** and cost alerts
- **Full procurement workflow** - suppliers, POs, deliveries
- **HAL+JSON hypermedia** API responses

## Project Structure

```
lightspeed-clone/
├── src/
│   ├── Shared/
│   │   ├── Shared.Contracts/      # DTOs, Events, HAL helpers
│   │   └── Shared.Infrastructure/ # Base entities, common patterns
│   └── Services/
│       ├── Auth/Auth.Api/
│       ├── Location/Location.Api/
│       ├── Menu/Menu.Api/
│       ├── Orders/Orders.Api/
│       ├── Payments/Payments.Api/
│       ├── Hardware/Hardware.Api/
│       ├── Inventory/Inventory.Api/
│       ├── Procurement/Procurement.Api/
│       ├── Costing/Costing.Api/
│       └── Reporting/Reporting.Api/
├── tests/
│   ├── Tests.Shared/
│   ├── Auth.Tests/
│   ├── Location.Tests/
│   ├── Menu.Tests/
│   ├── Orders.Tests/
│   ├── Payments.Tests/
│   ├── Hardware.Tests/
│   ├── Inventory.Tests/
│   ├── Procurement.Tests/
│   ├── Costing.Tests/
│   └── Reporting.Tests/
├── apps/
│   ├── pos/                       # React PWA (planned)
│   └── backoffice/                # React SPA (planned)
└── docker/
    └── docker-compose.yml         # Local infrastructure
```

## Prerequisites

- .NET 10 SDK
- PostgreSQL (for production) or SQLite (for development/testing)
- Node.js 20+ (for frontend apps)

## Getting Started

### Run Tests

```bash
# Run all tests
dotnet test

# Run specific service tests
dotnet test tests/Costing.Tests
```

### Build All Services

```bash
dotnet build
```

### Run a Service

```bash
cd src/Services/Menu/Menu.Api
dotnet run
```

## Test Coverage

| Service | Tests |
|---------|-------|
| Auth | 13 |
| Menu | 31 |
| Orders | 19 |
| Inventory | 37 |
| Procurement | 42 |
| Payments | 35 |
| Hardware | 40 |
| Reporting | 81 |
| Location | 53 |
| Costing | 102 |
| **Total** | **453** |

## API Examples

### Create a Recipe

```bash
curl -X POST http://localhost:5000/api/recipes \
  -H "Content-Type: application/json" \
  -d '{
    "menuItemId": "uuid",
    "menuItemName": "Classic Burger",
    "code": "RCP-BURGER-001",
    "portionYield": 1
  }'
```

### Add Ingredient to Recipe

```bash
curl -X POST http://localhost:5000/api/recipes/{recipeId}/ingredients \
  -H "Content-Type: application/json" \
  -d '{
    "ingredientId": "uuid",
    "ingredientName": "Ground Beef",
    "quantity": 0.15,
    "unitOfMeasure": "kg",
    "wastePercentage": 5
  }'
```

### Calculate Recipe Cost

```bash
curl http://localhost:5000/api/recipes/{recipeId}/cost?menuPrice=12.00
```

## Technology Stack

- **.NET 10** - Backend APIs
- **Entity Framework Core 10** - ORM
- **PostgreSQL** - Production database
- **SQLite** - Development/testing
- **xUnit** - Testing framework
- **FluentAssertions** - Test assertions
- **React** - Frontend (planned)
- **sql.js** - Offline SQLite for PWA (planned)

## License

MIT
