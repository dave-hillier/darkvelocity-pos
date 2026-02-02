using Azure.Data.Tables;
using DarkVelocity.Host.Endpoints;
using DarkVelocity.Host.Extensions;
using DarkVelocity.Host.Services;
using DarkVelocity.Host.Streams;
using Orleans.Streams.Kafka.Config;

var builder = WebApplication.CreateBuilder(args);

// Azure Storage connection - uses Azurite for local dev, Azure Table Storage in production
var azureStorageConnectionString = builder.Configuration.GetConnectionString("AzureStorage")
    ?? "UseDevelopmentStorage=true";

// Kafka connection - uses local Docker for dev, Azure Event Hubs (Kafka protocol) in production
var kafkaBrokers = builder.Configuration.GetConnectionString("Kafka")
    ?? "localhost:9092";

// Configure Orleans Silo
builder.Host.UseOrleans(siloBuilder =>
{
    siloBuilder.UseLocalhostClustering();
    siloBuilder.AddMemoryGrainStorageAsDefault();
    siloBuilder.AddMemoryGrainStorage("PersistentStorage");
    siloBuilder.AddMemoryGrainStorage("OrleansStorage");

    // Azure Table Storage for JournaledGrain event sourcing
    siloBuilder.AddLogStorageBasedLogConsistencyProvider("LogStorage");
    siloBuilder.AddAzureTableGrainStorage("LogStorage", options =>
    {
        options.TableServiceClient = new TableServiceClient(azureStorageConnectionString);
    });

    // PubSubStore required for stream pub/sub
    siloBuilder.AddMemoryGrainStorage("PubSubStore");

    // Kafka streaming provider - stream namespaces map to Kafka topics
    siloBuilder.AddKafka(StreamConstants.DefaultStreamProvider)
        .WithOptions(options =>
        {
            options.BrokerList = [kafkaBrokers];
            options.ConsumerGroupId = "darkvelocity-orleans";
            options.ConsumeMode = ConsumeMode.LastCommittedMessage;

            // Register topics for each stream namespace
            options.AddTopic(StreamConstants.OrderStreamNamespace);
            options.AddTopic(StreamConstants.PaymentStreamNamespace);
            options.AddTopic(StreamConstants.InventoryStreamNamespace);
            options.AddTopic(StreamConstants.CustomerStreamNamespace);
            options.AddTopic(StreamConstants.BookingStreamNamespace);
            options.AddTopic(StreamConstants.EmployeeStreamNamespace);
            options.AddTopic(StreamConstants.UserStreamNamespace);
            options.AddTopic(StreamConstants.GiftCardStreamNamespace);
            options.AddTopic(StreamConstants.CustomerSpendStreamNamespace);
            options.AddTopic(StreamConstants.AccountingStreamNamespace);
            options.AddTopic(StreamConstants.SalesStreamNamespace);
            options.AddTopic(StreamConstants.AlertStreamNamespace);
            options.AddTopic(StreamConstants.DeviceStreamNamespace);
            options.AddTopic(StreamConstants.PurchaseDocumentStreamNamespace);
            options.AddTopic(StreamConstants.WorkflowStreamNamespace);
        })
        .Build();

    siloBuilder.UseDashboard(options =>
    {
        options.Port = 8888;
        options.HostSelf = true;
    });
});

// Configure services
builder.Services
    .AddSwaggerDocumentation()
    .AddJwtAuthentication(builder.Configuration)
    .AddCorsPolicy()
    .AddSearchServices(builder.Configuration)
    .AddPaymentGatewayServices()
    .AddSingleton<IDocumentIntelligenceService, StubDocumentIntelligenceService>()
    .AddSingleton<IEmailIngestionService, StubEmailIngestionService>()
    .AddSingleton<IFuzzyMatchingService, FuzzyMatchingService>();

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "DarkVelocity POS API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
   .WithTags("Health");

// Map all API endpoints
app.MapOAuthEndpoints()
   .MapAuthEndpoints()
   .MapDeviceEndpoints()
   .MapOrganizationEndpoints()
   .MapSiteEndpoints()
   .MapOrderEndpoints()
   .MapPaymentEndpoints()
   .MapMenuEndpoints()
   .MapMenuCmsEndpoints()
   .MapCustomerEndpoints()
   .MapInventoryEndpoints()
   .MapBookingEndpoints()
   .MapEmployeeEndpoints()
   .MapSearchEndpoints()
   .MapTableEndpoints()
   .MapFloorPlanEndpoints()
   .MapWaitlistEndpoints()
   .MapAvailabilityEndpoints()
   .MapWebhookEndpoints()
   .MapPaymentGatewayEndpoints()
   .MapChannelEndpoints()
   .MapBatchEndpoints()
   .MapPurchaseDocumentEndpoints()
   .MapEmailIngestionEndpoints()
   .MapVendorMappingEndpoints()
   .MapExpenseEndpoints();

app.Run();

// Expose Program class for WebApplicationFactory integration testing
public partial class Program { }
