using System.ComponentModel.DataAnnotations;
using DarkVelocity.Host.Events;
using Microsoft.Extensions.Options;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Fiskaly App-Level Configuration (appsettings.json)
// ============================================================================

/// <summary>
/// Application-level Fiskaly configuration loaded from appsettings.json.
/// Configure in appsettings.json under "Fiskaly" section.
/// </summary>
public sealed class FiskalyOptions
{
    public const string SectionName = "Fiskaly";

    /// <summary>
    /// Whether Fiskaly integration is globally enabled
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Default region for new tenants
    /// </summary>
    public string DefaultRegion { get; set; } = "Germany";

    /// <summary>
    /// Default environment (Test or Production)
    /// </summary>
    public string DefaultEnvironment { get; set; } = "Test";

    /// <summary>
    /// HTTP client timeout in seconds
    /// </summary>
    public int HttpTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Number of retry attempts for failed API calls
    /// </summary>
    public int RetryAttempts { get; set; } = 3;

    /// <summary>
    /// Delay between retries in milliseconds
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Whether to use exponential backoff for retries
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Whether to automatically subscribe to TSE events on activation
    /// </summary>
    public bool AutoSubscribeToEvents { get; set; } = true;

    /// <summary>
    /// Token refresh buffer in minutes (refresh before expiry)
    /// </summary>
    public int TokenRefreshBufferMinutes { get; set; } = 5;

    /// <summary>
    /// Pre-configured tenant credentials (for development/testing)
    /// </summary>
    public Dictionary<string, FiskalyTenantOptions> Tenants { get; set; } = new();

    /// <summary>
    /// Region-specific API configurations
    /// </summary>
    public FiskalyRegionOptions Regions { get; set; } = new();
}

/// <summary>
/// Per-tenant Fiskaly options from configuration
/// </summary>
public sealed class FiskalyTenantOptions
{
    /// <summary>
    /// Whether this tenant has Fiskaly enabled
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Region for this tenant
    /// </summary>
    public string Region { get; set; } = "Germany";

    /// <summary>
    /// Environment (Test or Production)
    /// </summary>
    public string Environment { get; set; } = "Test";

    /// <summary>
    /// Fiskaly API Key
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Fiskaly API Secret
    /// </summary>
    public string? ApiSecret { get; set; }

    /// <summary>
    /// TSS ID for Germany KassenSichV
    /// </summary>
    public string? TssId { get; set; }

    /// <summary>
    /// Client ID (POS identifier)
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Organization ID in Fiskaly
    /// </summary>
    public string? OrganizationId { get; set; }

    /// <summary>
    /// Whether to forward all TSE events to Fiskaly
    /// </summary>
    public bool ForwardAllEvents { get; set; } = true;

    /// <summary>
    /// Whether to require external signature from Fiskaly
    /// </summary>
    public bool RequireExternalSignature { get; set; } = true;
}

/// <summary>
/// Region-specific API configurations
/// </summary>
public sealed class FiskalyRegionOptions
{
    public FiskalyRegionEndpoints Germany { get; set; } = new()
    {
        TestUrl = "https://kassensichv-middleware.fiskaly.com/api/v2",
        ProductionUrl = "https://kassensichv-middleware.fiskaly.com/api/v2"
    };

    public FiskalyRegionEndpoints Austria { get; set; } = new()
    {
        TestUrl = "https://rksv.fiskaly.com/api/v1",
        ProductionUrl = "https://rksv.fiskaly.com/api/v1"
    };

    public FiskalyRegionEndpoints Italy { get; set; } = new()
    {
        TestUrl = "https://rt.fiskaly.com/api/v1",
        ProductionUrl = "https://rt.fiskaly.com/api/v1"
    };
}

/// <summary>
/// Region endpoint configuration
/// </summary>
public sealed class FiskalyRegionEndpoints
{
    public string TestUrl { get; set; } = string.Empty;
    public string ProductionUrl { get; set; } = string.Empty;
}

// ============================================================================
// Fiskaly Tenant Configuration Grain
// ============================================================================

/// <summary>
/// Command to update tenant Fiskaly configuration
/// </summary>
[GenerateSerializer]
public sealed record UpdateFiskalyTenantConfigCommand(
    [property: Id(0)] bool Enabled,
    [property: Id(1)] FiskalyRegion Region,
    [property: Id(2)] FiskalyEnvironment Environment,
    [property: Id(3)] string? ApiKey,
    [property: Id(4)] string? ApiSecret,
    [property: Id(5)] string? TssId,
    [property: Id(6)] string? ClientId,
    [property: Id(7)] string? OrganizationId,
    [property: Id(8)] bool ForwardAllEvents,
    [property: Id(9)] bool RequireExternalSignature);

/// <summary>
/// Tenant Fiskaly configuration snapshot
/// </summary>
[GenerateSerializer]
public sealed record FiskalyTenantConfigSnapshot(
    [property: Id(0)] Guid TenantId,
    [property: Id(1)] bool Enabled,
    [property: Id(2)] FiskalyRegion Region,
    [property: Id(3)] FiskalyEnvironment Environment,
    [property: Id(4)] bool HasCredentials,
    [property: Id(5)] string? TssId,
    [property: Id(6)] string? ClientId,
    [property: Id(7)] string? OrganizationId,
    [property: Id(8)] bool ForwardAllEvents,
    [property: Id(9)] bool RequireExternalSignature,
    [property: Id(10)] DateTime? LastUpdatedAt,
    [property: Id(11)] int Version);

/// <summary>
/// Grain for managing per-tenant Fiskaly configuration.
/// Key: "{orgId}:fiskaly:config"
/// </summary>
public interface IFiskalyConfigGrain : IGrainWithStringKey
{
    /// <summary>
    /// Get current configuration
    /// </summary>
    Task<FiskalyTenantConfigSnapshot> GetConfigAsync();

    /// <summary>
    /// Update configuration
    /// </summary>
    Task<FiskalyTenantConfigSnapshot> UpdateConfigAsync(UpdateFiskalyTenantConfigCommand command);

    /// <summary>
    /// Get the full configuration for use by integration grain
    /// </summary>
    Task<FiskalyConfiguration?> GetFiskalyConfigurationAsync();

    /// <summary>
    /// Enable Fiskaly integration
    /// </summary>
    Task<FiskalyTenantConfigSnapshot> EnableAsync();

    /// <summary>
    /// Disable Fiskaly integration
    /// </summary>
    Task<FiskalyTenantConfigSnapshot> DisableAsync();

    /// <summary>
    /// Validate current configuration
    /// </summary>
    Task<FiskalyConfigValidationResult> ValidateAsync();
}

/// <summary>
/// Result of configuration validation
/// </summary>
[GenerateSerializer]
public sealed record FiskalyConfigValidationResult(
    [property: Id(0)] bool IsValid,
    [property: Id(1)] List<string> Errors,
    [property: Id(2)] List<string> Warnings);

/// <summary>
/// State for Fiskaly tenant configuration
/// </summary>
[GenerateSerializer]
public sealed class FiskalyTenantConfigState
{
    [Id(0)] public Guid TenantId { get; set; }
    [Id(1)] public bool Enabled { get; set; }
    [Id(2)] public FiskalyRegion Region { get; set; } = FiskalyRegion.Germany;
    [Id(3)] public FiskalyEnvironment Environment { get; set; } = FiskalyEnvironment.Test;
    [Id(4)] public string? ApiKey { get; set; }
    [Id(5)] public string? ApiSecret { get; set; }
    [Id(6)] public string? TssId { get; set; }
    [Id(7)] public string? ClientId { get; set; }
    [Id(8)] public string? OrganizationId { get; set; }
    [Id(9)] public bool ForwardAllEvents { get; set; } = true;
    [Id(10)] public bool RequireExternalSignature { get; set; } = true;
    [Id(11)] public DateTime? LastUpdatedAt { get; set; }
    [Id(12)] public int Version { get; set; }
}

/// <summary>
/// Grain implementation for per-tenant Fiskaly configuration
/// </summary>
public sealed class FiskalyConfigGrain : Grain, IFiskalyConfigGrain
{
    private readonly IPersistentState<FiskalyTenantConfigState> _state;
    private readonly IOptions<FiskalyOptions> _options;

    public FiskalyConfigGrain(
        [PersistentState("fiskalyConfig", "OrleansStorage")]
        IPersistentState<FiskalyTenantConfigState> state,
        IOptions<FiskalyOptions> options)
    {
        _state = state;
        _options = options;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // Initialize from app configuration if not yet configured
        if (_state.State.Version == 0)
        {
            var key = this.GetPrimaryKeyString();
            var parts = key.Split(':');
            var tenantId = Guid.Parse(parts[0]);

            _state.State.TenantId = tenantId;

            // Check if there's a pre-configured tenant in app settings
            if (_options.Value.Tenants.TryGetValue(tenantId.ToString(), out var tenantOptions))
            {
                ApplyTenantOptions(tenantOptions);
            }
            else
            {
                // Apply defaults from app configuration
                _state.State.Region = ParseRegion(_options.Value.DefaultRegion);
                _state.State.Environment = ParseEnvironment(_options.Value.DefaultEnvironment);
            }
        }

        return base.OnActivateAsync(cancellationToken);
    }

    public Task<FiskalyTenantConfigSnapshot> GetConfigAsync()
    {
        return Task.FromResult(CreateSnapshot());
    }

    public async Task<FiskalyTenantConfigSnapshot> UpdateConfigAsync(UpdateFiskalyTenantConfigCommand command)
    {
        _state.State.Enabled = command.Enabled;
        _state.State.Region = command.Region;
        _state.State.Environment = command.Environment;
        _state.State.ApiKey = command.ApiKey;
        _state.State.ApiSecret = command.ApiSecret;
        _state.State.TssId = command.TssId;
        _state.State.ClientId = command.ClientId;
        _state.State.OrganizationId = command.OrganizationId;
        _state.State.ForwardAllEvents = command.ForwardAllEvents;
        _state.State.RequireExternalSignature = command.RequireExternalSignature;
        _state.State.LastUpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();

        return CreateSnapshot();
    }

    public Task<FiskalyConfiguration?> GetFiskalyConfigurationAsync()
    {
        if (!_state.State.Enabled ||
            string.IsNullOrEmpty(_state.State.ApiKey) ||
            string.IsNullOrEmpty(_state.State.ApiSecret))
        {
            return Task.FromResult<FiskalyConfiguration?>(null);
        }

        var config = new FiskalyConfiguration(
            Enabled: _state.State.Enabled,
            Region: _state.State.Region,
            Environment: _state.State.Environment,
            ApiKey: _state.State.ApiKey,
            ApiSecret: _state.State.ApiSecret,
            TssId: _state.State.TssId,
            ClientId: _state.State.ClientId,
            OrganizationId: _state.State.OrganizationId);

        return Task.FromResult<FiskalyConfiguration?>(config);
    }

    public async Task<FiskalyTenantConfigSnapshot> EnableAsync()
    {
        _state.State.Enabled = true;
        _state.State.LastUpdatedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<FiskalyTenantConfigSnapshot> DisableAsync()
    {
        _state.State.Enabled = false;
        _state.State.LastUpdatedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public Task<FiskalyConfigValidationResult> ValidateAsync()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (_state.State.Enabled)
        {
            if (string.IsNullOrEmpty(_state.State.ApiKey))
                errors.Add("API Key is required when Fiskaly is enabled");

            if (string.IsNullOrEmpty(_state.State.ApiSecret))
                errors.Add("API Secret is required when Fiskaly is enabled");

            if (_state.State.Region == FiskalyRegion.Germany && string.IsNullOrEmpty(_state.State.TssId))
                errors.Add("TSS ID is required for Germany (KassenSichV)");

            if (string.IsNullOrEmpty(_state.State.ClientId))
                warnings.Add("Client ID is recommended for transaction tracking");

            if (_state.State.Environment == FiskalyEnvironment.Test)
                warnings.Add("Configuration is set to Test environment");
        }

        return Task.FromResult(new FiskalyConfigValidationResult(
            IsValid: errors.Count == 0,
            Errors: errors,
            Warnings: warnings));
    }

    private void ApplyTenantOptions(FiskalyTenantOptions options)
    {
        _state.State.Enabled = options.Enabled;
        _state.State.Region = ParseRegion(options.Region);
        _state.State.Environment = ParseEnvironment(options.Environment);
        _state.State.ApiKey = options.ApiKey;
        _state.State.ApiSecret = options.ApiSecret;
        _state.State.TssId = options.TssId;
        _state.State.ClientId = options.ClientId;
        _state.State.OrganizationId = options.OrganizationId;
        _state.State.ForwardAllEvents = options.ForwardAllEvents;
        _state.State.RequireExternalSignature = options.RequireExternalSignature;
    }

    private FiskalyTenantConfigSnapshot CreateSnapshot()
    {
        return new FiskalyTenantConfigSnapshot(
            TenantId: _state.State.TenantId,
            Enabled: _state.State.Enabled,
            Region: _state.State.Region,
            Environment: _state.State.Environment,
            HasCredentials: !string.IsNullOrEmpty(_state.State.ApiKey) && !string.IsNullOrEmpty(_state.State.ApiSecret),
            TssId: _state.State.TssId,
            ClientId: _state.State.ClientId,
            OrganizationId: _state.State.OrganizationId,
            ForwardAllEvents: _state.State.ForwardAllEvents,
            RequireExternalSignature: _state.State.RequireExternalSignature,
            LastUpdatedAt: _state.State.LastUpdatedAt,
            Version: _state.State.Version);
    }

    private static FiskalyRegion ParseRegion(string region) => region?.ToLowerInvariant() switch
    {
        "germany" or "de" => FiskalyRegion.Germany,
        "austria" or "at" => FiskalyRegion.Austria,
        "italy" or "it" => FiskalyRegion.Italy,
        _ => FiskalyRegion.Germany
    };

    private static FiskalyEnvironment ParseEnvironment(string env) => env?.ToLowerInvariant() switch
    {
        "production" or "prod" => FiskalyEnvironment.Production,
        "test" or "sandbox" or "dev" => FiskalyEnvironment.Test,
        _ => FiskalyEnvironment.Test
    };
}

// ============================================================================
// Fiskaly Configuration Events
// ============================================================================

/// <summary>
/// Published when tenant Fiskaly configuration is updated
/// </summary>
public sealed record FiskalyConfigUpdated(
    Guid TenantId,
    bool Enabled,
    string Region,
    string Environment,
    DateTime UpdatedAt
) : IntegrationEvent
{
    public override string EventType => "fiskaly.config.updated";
}

/// <summary>
/// Published when tenant Fiskaly configuration validation fails
/// </summary>
public sealed record FiskalyConfigValidationFailed(
    Guid TenantId,
    List<string> Errors,
    DateTime FailedAt
) : IntegrationEvent
{
    public override string EventType => "fiskaly.config.validation_failed";
}

// ============================================================================
// Service Registration Extensions
// ============================================================================

/// <summary>
/// Extension methods for registering Fiskaly services
/// </summary>
public static class FiskalyServiceExtensions
{
    /// <summary>
    /// Add Fiskaly integration services
    /// </summary>
    public static IServiceCollection AddFiskalyIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<FiskalyOptions>(configuration.GetSection(FiskalyOptions.SectionName));

        // Register HTTP client
        services.AddHttpClient<IFiskalyClient, FiskalyHttpClient>(client =>
        {
            var options = configuration.GetSection(FiskalyOptions.SectionName).Get<FiskalyOptions>();
            client.Timeout = TimeSpan.FromSeconds(options?.HttpTimeoutSeconds ?? 30);
        });

        return services;
    }

    /// <summary>
    /// Validate Fiskaly configuration on startup
    /// </summary>
    public static IServiceCollection AddFiskalyConfigurationValidation(this IServiceCollection services)
    {
        services.AddOptions<FiskalyOptions>()
            .ValidateDataAnnotations()
            .Validate(options =>
            {
                if (options.Enabled && options.RetryAttempts < 0)
                    return false;
                if (options.HttpTimeoutSeconds <= 0)
                    return false;
                return true;
            }, "Invalid Fiskaly configuration");

        return services;
    }
}

// ============================================================================
// Configuration-Aware Fiskaly Client
// ============================================================================

/// <summary>
/// Configuration-aware Fiskaly client that uses FiskalyOptions
/// </summary>
public sealed class ConfigAwareFiskalyClient : IFiskalyClient
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<FiskalyOptions> _options;
    private readonly System.Text.Json.JsonSerializerOptions _jsonOptions;

    public ConfigAwareFiskalyClient(HttpClient httpClient, IOptions<FiskalyOptions> options)
    {
        _httpClient = httpClient;
        _options = options;
        _jsonOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<FiskalyAuthResponse> AuthenticateAsync(FiskalyConfiguration config, CancellationToken ct = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var url = GetBaseUrl(config);
            var request = new HttpRequestMessage(HttpMethod.Post, $"{url}/auth")
            {
                Content = System.Net.Http.Json.JsonContent.Create(
                    new { api_key = config.ApiKey, api_secret = config.ApiSecret },
                    options: _jsonOptions)
            };

            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<FiskalyAuthResponse>(_jsonOptions, ct)
                ?? throw new InvalidOperationException("Failed to deserialize auth response");
        }, ct);
    }

    public async Task<FiskalyTssInfo> GetTssAsync(FiskalyConfiguration config, string accessToken, CancellationToken ct = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var url = GetBaseUrl(config);
            var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/tss/{config.TssId}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<FiskalyTssInfo>(_jsonOptions, ct)
                ?? throw new InvalidOperationException("Failed to deserialize TSS info");
        }, ct);
    }

    public async Task<FiskalyTransactionResponse> StartTransactionAsync(
        FiskalyConfiguration config,
        string accessToken,
        string transactionId,
        CancellationToken ct = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var url = GetBaseUrl(config);
            var requestUrl = $"{url}/tss/{config.TssId}/tx/{transactionId}?tx_revision=1";
            var request = new HttpRequestMessage(HttpMethod.Put, requestUrl)
            {
                Content = System.Net.Http.Json.JsonContent.Create(
                    new FiskalyStartTransactionRequest("ACTIVE", config.ClientId!),
                    options: _jsonOptions)
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<FiskalyTransactionResponse>(_jsonOptions, ct)
                ?? throw new InvalidOperationException("Failed to deserialize transaction response");
        }, ct);
    }

    public async Task<FiskalyTransactionResponse> FinishTransactionAsync(
        FiskalyConfiguration config,
        string accessToken,
        string transactionId,
        FiskalyReceipt receipt,
        CancellationToken ct = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var url = GetBaseUrl(config);
            var requestUrl = $"{url}/tss/{config.TssId}/tx/{transactionId}?tx_revision=2";
            var finishRequest = new FiskalyFinishTransactionRequest(
                "FINISHED",
                config.ClientId!,
                new FiskalyTransactionSchema(new FiskalyStandardV1(receipt)));

            var request = new HttpRequestMessage(HttpMethod.Put, requestUrl)
            {
                Content = System.Net.Http.Json.JsonContent.Create(finishRequest, options: _jsonOptions)
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<FiskalyTransactionResponse>(_jsonOptions, ct)
                ?? throw new InvalidOperationException("Failed to deserialize transaction response");
        }, ct);
    }

    public async Task<FiskalyTransactionResponse> SignReceiptAsync(
        FiskalyConfiguration config,
        string accessToken,
        FiskalyRksvReceiptRequest receipt,
        CancellationToken ct = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var url = GetBaseUrl(config);
            var requestUrl = $"{url}/cash-registers/{config.ClientId}/receipts";
            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = System.Net.Http.Json.JsonContent.Create(receipt, options: _jsonOptions)
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<FiskalyTransactionResponse>(_jsonOptions, ct)
                ?? throw new InvalidOperationException("Failed to deserialize receipt response");
        }, ct);
    }

    private string GetBaseUrl(FiskalyConfiguration config)
    {
        var regions = _options.Value.Regions;
        var endpoints = config.Region switch
        {
            FiskalyRegion.Germany => regions.Germany,
            FiskalyRegion.Austria => regions.Austria,
            FiskalyRegion.Italy => regions.Italy,
            _ => regions.Germany
        };

        return config.Environment == FiskalyEnvironment.Production
            ? endpoints.ProductionUrl
            : endpoints.TestUrl;
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        var retryAttempts = _options.Value.RetryAttempts;
        var retryDelayMs = _options.Value.RetryDelayMs;
        var useExponentialBackoff = _options.Value.UseExponentialBackoff;

        Exception? lastException = null;

        for (var attempt = 0; attempt <= retryAttempts; attempt++)
        {
            try
            {
                return await action();
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;

                if (attempt < retryAttempts)
                {
                    var delay = useExponentialBackoff
                        ? retryDelayMs * (int)Math.Pow(2, attempt)
                        : retryDelayMs;

                    await Task.Delay(delay, ct);
                }
            }
        }

        throw lastException!;
    }
}
