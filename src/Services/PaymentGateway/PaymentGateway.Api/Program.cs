using DarkVelocity.PaymentGateway.Api.Data;
using DarkVelocity.PaymentGateway.Api.Middleware;
using DarkVelocity.PaymentGateway.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "DarkVelocity Payment Gateway API",
        Version = "v1",
        Description = "A Stripe-like payment gateway with HAL REST API supporting POS and eCommerce transactions"
    });

    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "API Key authentication using Bearer scheme. Enter your API key (e.g., sk_test_xxx)",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Database
if (builder.Environment.IsEnvironment("Test"))
{
    builder.Services.AddDbContext<PaymentGatewayDbContext>(options =>
        options.UseSqlite("DataSource=:memory:"));
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("PaymentGatewayDb")
        ?? "Host=localhost;Database=payment_gateway_db;Username=postgres;Password=postgres";

    builder.Services.AddDbContext<PaymentGatewayDbContext>(options =>
        options.UseNpgsql(connectionString));
}

// Services
builder.Services.AddScoped<KeyGenerationService>();
builder.Services.AddScoped<WebhookService>();
builder.Services.AddScoped<PaymentProcessingService>();

// HTTP Client for webhooks
builder.Services.AddHttpClient();

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<PaymentGatewayDbContext>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Payment Gateway API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

// API Key Authentication
app.UseApiKeyAuthentication();

app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
