using DarkVelocity.Costing.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
if (builder.Environment.IsEnvironment("Test"))
{
    builder.Services.AddDbContext<CostingDbContext>(options =>
        options.UseSqlite("DataSource=:memory:"));
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("CostingDb")
        ?? "Host=localhost;Database=costing_db;Username=postgres;Password=postgres";

    builder.Services.AddDbContext<CostingDbContext>(options =>
        options.UseNpgsql(connectionString));
}

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<CostingDbContext>();

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
