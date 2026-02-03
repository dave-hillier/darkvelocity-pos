using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Services;

/// <summary>
/// Configuration for seeding a bootstrap API key.
/// </summary>
public sealed class ApiKeySeedOptions
{
    public const string SectionName = "ApiKeySeed";

    /// <summary>
    /// Whether to enable API key seeding on startup.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Organization ID to create the key for.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// User ID that owns the key.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Name for the bootstrap key.
    /// </summary>
    public string Name { get; set; } = "Bootstrap Admin Key";

    /// <summary>
    /// Description for the key.
    /// </summary>
    public string? Description { get; set; } = "Initial setup key - rotate after provisioning";

    /// <summary>
    /// Whether this is a test mode key.
    /// </summary>
    public bool IsTestMode { get; set; }

    /// <summary>
    /// Hours until the key expires. Default is 24 hours.
    /// </summary>
    public int ExpiresInHours { get; set; } = 24;

    /// <summary>
    /// Roles to grant to the key.
    /// </summary>
    public List<string> Roles { get; set; } = ["owner", "admin", "manager", "backoffice"];

    /// <summary>
    /// IP ranges to restrict the key to (optional).
    /// </summary>
    public List<string>? AllowedIpRanges { get; set; }

    /// <summary>
    /// File path to write the generated key to (optional).
    /// If not set, key is written to console/logs.
    /// </summary>
    public string? OutputFile { get; set; }
}

/// <summary>
/// Hosted service that seeds a bootstrap API key on startup.
/// Use this for initial setup or development environments.
/// </summary>
public sealed class ApiKeySeeder : IHostedService
{
    private readonly IGrainFactory _grainFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiKeySeeder> _logger;

    public ApiKeySeeder(
        IGrainFactory grainFactory,
        IConfiguration configuration,
        ILogger<ApiKeySeeder> logger)
    {
        _grainFactory = grainFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var options = new ApiKeySeedOptions();
        _configuration.GetSection(ApiKeySeedOptions.SectionName).Bind(options);

        if (!options.Enabled)
        {
            _logger.LogDebug("API key seeding is disabled");
            return;
        }

        if (options.OrganizationId == Guid.Empty)
        {
            _logger.LogWarning("API key seeding enabled but OrganizationId not configured");
            return;
        }

        if (options.UserId == Guid.Empty)
        {
            _logger.LogWarning("API key seeding enabled but UserId not configured");
            return;
        }

        try
        {
            await SeedApiKeyAsync(options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed API key");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedApiKeyAsync(ApiKeySeedOptions options, CancellationToken cancellationToken)
    {
        // Check if user already has API keys (avoid duplicate seeding)
        var registryGrain = _grainFactory.GetGrain<IApiKeyRegistryGrain>(
            GrainKeys.ApiKeyRegistry(options.OrganizationId, options.UserId));

        if (await registryGrain.ExistsAsync())
        {
            var existingKeys = await registryGrain.GetKeyIdsAsync();
            if (existingKeys.Count > 0)
            {
                _logger.LogInformation(
                    "User {UserId} already has {Count} API key(s), skipping seed",
                    options.UserId, existingKeys.Count);
                return;
            }
        }
        else
        {
            await registryGrain.InitializeAsync(options.OrganizationId, options.UserId);
        }

        // Create the bootstrap key
        var keyId = Guid.NewGuid();
        var keyGrain = _grainFactory.GetGrain<IApiKeyGrain>(
            GrainKeys.ApiKey(options.OrganizationId, keyId));

        var result = await keyGrain.CreateAsync(new CreateApiKeyCommand(
            OrganizationId: options.OrganizationId,
            UserId: options.UserId,
            Name: options.Name,
            Description: options.Description,
            Type: ApiKeyType.Secret,
            IsTestMode: options.IsTestMode,
            Scopes: null, // Full access for bootstrap
            CustomClaims: new Dictionary<string, string>
            {
                ["purpose"] = "bootstrap",
                ["seeded_at"] = DateTime.UtcNow.ToString("O")
            },
            Roles: options.Roles,
            AllowedSiteIds: null, // All sites
            AllowedIpRanges: options.AllowedIpRanges,
            RateLimitPerMinute: null,
            ExpiresAt: DateTime.UtcNow.AddHours(options.ExpiresInHours)));

        _logger.LogWarning(
            "Bootstrap API key created for org {OrgId}, user {UserId}. " +
            "Key expires at {ExpiresAt}. ROTATE THIS KEY AFTER SETUP.",
            options.OrganizationId, options.UserId,
            DateTime.UtcNow.AddHours(options.ExpiresInHours));

        // Output the key
        if (!string.IsNullOrEmpty(options.OutputFile))
        {
            await File.WriteAllTextAsync(options.OutputFile, result.ApiKey, cancellationToken);
            _logger.LogInformation("API key written to {OutputFile}", options.OutputFile);
        }
        else
        {
            // Log to console (will be visible in startup logs)
            _logger.LogWarning(
                "========================================\n" +
                "BOOTSTRAP API KEY (save this, shown only once):\n" +
                "{ApiKey}\n" +
                "========================================",
                result.ApiKey);
        }
    }
}

/// <summary>
/// Extension methods for registering the API key seeder.
/// </summary>
public static class ApiKeySeederExtensions
{
    /// <summary>
    /// Adds the API key seeder hosted service.
    /// Configure with the "ApiKeySeed" configuration section.
    /// </summary>
    public static IServiceCollection AddApiKeySeeder(this IServiceCollection services)
    {
        services.AddHostedService<ApiKeySeeder>();
        return services;
    }
}
