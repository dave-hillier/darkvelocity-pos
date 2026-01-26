using DarkVelocity.Hardware.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
if (builder.Environment.IsEnvironment("Test"))
{
    builder.Services.AddDbContext<HardwareDbContext>(options =>
        options.UseSqlite("DataSource=:memory:"));
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("HardwareDb")
        ?? "Host=localhost;Database=hardware_db;Username=postgres;Password=postgres";

    builder.Services.AddDbContext<HardwareDbContext>(options =>
        options.UseNpgsql(connectionString));
}

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<HardwareDbContext>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
