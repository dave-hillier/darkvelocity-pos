using DarkVelocity.Inventory.Api.Data;
using DarkVelocity.Inventory.Api.Services;
using DarkVelocity.Shared.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database - only register if not in Test environment (tests configure their own provider)
if (!builder.Environment.IsEnvironment("Test"))
{
    builder.Services.AddDbContext<InventoryDbContext>(options =>
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=inventory_db;Username=darkvelocity;Password=darkvelocity_dev";
        options.UseNpgsql(connectionString);
    });
}

// Event Bus
builder.Services.AddInMemoryEventBus();
builder.Services.AddEventHandlersFromAssembly(typeof(Program).Assembly);

// Services
builder.Services.AddScoped<IFifoConsumptionService, FifoConsumptionService>();
builder.Services.AddScoped<IRecipeCostService, RecipeCostService>();

builder.Services.AddControllers();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<InventoryDbContext>();

var app = builder.Build();

// Migrate database on startup in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    db.Database.EnsureCreated();
}

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Make Program accessible for WebApplicationFactory
public partial class Program { }
