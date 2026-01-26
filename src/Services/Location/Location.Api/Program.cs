using DarkVelocity.Location.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
if (builder.Environment.IsEnvironment("Test"))
{
    builder.Services.AddDbContext<LocationDbContext>(options =>
        options.UseSqlite("DataSource=:memory:"));
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("LocationDb")
        ?? "Host=localhost;Database=location_db;Username=postgres;Password=postgres";

    builder.Services.AddDbContext<LocationDbContext>(options =>
        options.UseNpgsql(connectionString));
}

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<LocationDbContext>();

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
