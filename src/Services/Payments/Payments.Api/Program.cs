using DarkVelocity.Payments.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
if (builder.Environment.IsEnvironment("Test"))
{
    builder.Services.AddDbContext<PaymentsDbContext>(options =>
        options.UseSqlite("DataSource=:memory:"));
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("PaymentsDb")
        ?? "Host=localhost;Database=payments_db;Username=postgres;Password=postgres";

    builder.Services.AddDbContext<PaymentsDbContext>(options =>
        options.UseNpgsql(connectionString));
}

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<PaymentsDbContext>();

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
