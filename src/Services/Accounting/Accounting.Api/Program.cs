using DarkVelocity.Accounting.Api.Data;
using DarkVelocity.Accounting.Api.Services;
using DarkVelocity.Shared.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database setup - check Test environment
if (!builder.Environment.IsEnvironment("Test"))
{
    builder.Services.AddDbContext<AccountingDbContext>(options =>
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=accounting_db;Username=darkvelocity;Password=darkvelocity_dev";
        options.UseNpgsql(connectionString);
    });
}

// Services
builder.Services.AddScoped<IJournalEntryNumberGenerator, JournalEntryNumberGenerator>();

// Event Bus
builder.Services.AddInMemoryEventBus();
builder.Services.AddEventHandlersFromAssembly(typeof(Program).Assembly);

builder.Services.AddControllers();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AccountingDbContext>();

var app = builder.Build();

// Development only - auto-migrate/create database
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AccountingDbContext>();
    db.Database.EnsureCreated();
}

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
