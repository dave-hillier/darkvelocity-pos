using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DarkVelocity.Host.Events;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Fiskaly Region Configuration
// Supports Germany (KassenSichV), Austria (RKSV), Italy (RT)
// ============================================================================

/// <summary>
/// Supported Fiskaly regions
/// </summary>
public enum FiskalyRegion
{
    /// <summary>Germany - KassenSichV compliance</summary>
    Germany,
    /// <summary>Austria - RKSV (Registrierkassensicherheitsverordnung)</summary>
    Austria,
    /// <summary>Italy - RT (Registratore Telematico)</summary>
    Italy
}

/// <summary>
/// Fiskaly API environment
/// </summary>
public enum FiskalyEnvironment
{
    /// <summary>Test/sandbox environment</summary>
    Test,
    /// <summary>Production environment</summary>
    Production
}

/// <summary>
/// Configuration for Fiskaly integration
/// </summary>
[GenerateSerializer]
public sealed record FiskalyConfiguration(
    [property: Id(0)] bool Enabled,
    [property: Id(1)] FiskalyRegion Region,
    [property: Id(2)] FiskalyEnvironment Environment,
    [property: Id(3)] string ApiKey,
    [property: Id(4)] string ApiSecret,
    [property: Id(5)] string? TssId,
    [property: Id(6)] string? ClientId,
    [property: Id(7)] string? OrganizationId)
{
    /// <summary>
    /// Get the base URL for the Fiskaly API based on region and environment
    /// </summary>
    public string GetBaseUrl() => (Region, Environment) switch
    {
        (FiskalyRegion.Germany, FiskalyEnvironment.Test) => "https://kassensichv-middleware.fiskaly.com/api/v2",
        (FiskalyRegion.Germany, FiskalyEnvironment.Production) => "https://kassensichv-middleware.fiskaly.com/api/v2",
        (FiskalyRegion.Austria, FiskalyEnvironment.Test) => "https://rksv.fiskaly.com/api/v1",
        (FiskalyRegion.Austria, FiskalyEnvironment.Production) => "https://rksv.fiskaly.com/api/v1",
        (FiskalyRegion.Italy, FiskalyEnvironment.Test) => "https://rt.fiskaly.com/api/v1",
        (FiskalyRegion.Italy, FiskalyEnvironment.Production) => "https://rt.fiskaly.com/api/v1",
        _ => throw new ArgumentOutOfRangeException()
    };
}

// ============================================================================
// Fiskaly API DTOs
// ============================================================================

/// <summary>
/// Fiskaly authentication response
/// </summary>
[GenerateSerializer]
public sealed record FiskalyAuthResponse(
    [property: Id(0), JsonPropertyName("access_token")] string AccessToken,
    [property: Id(1), JsonPropertyName("access_token_expires_at")] long ExpiresAt);

/// <summary>
/// Fiskaly TSS (Technical Security System) info
/// </summary>
[GenerateSerializer]
public sealed record FiskalyTssInfo(
    [property: Id(0), JsonPropertyName("_id")] string Id,
    [property: Id(1), JsonPropertyName("state")] string State,
    [property: Id(2), JsonPropertyName("serial_number")] string? SerialNumber,
    [property: Id(3), JsonPropertyName("certificate")] string? Certificate,
    [property: Id(4), JsonPropertyName("public_key")] string? PublicKey);

/// <summary>
/// Fiskaly transaction start request (Germany)
/// </summary>
[GenerateSerializer]
public sealed record FiskalyStartTransactionRequest(
    [property: Id(0), JsonPropertyName("state")] string State,
    [property: Id(1), JsonPropertyName("client_id")] string ClientId);

/// <summary>
/// Fiskaly transaction finish request (Germany)
/// </summary>
[GenerateSerializer]
public sealed record FiskalyFinishTransactionRequest(
    [property: Id(0), JsonPropertyName("state")] string State,
    [property: Id(1), JsonPropertyName("client_id")] string ClientId,
    [property: Id(2), JsonPropertyName("schema")] FiskalyTransactionSchema Schema);

/// <summary>
/// Fiskaly transaction schema (KassenSichV format)
/// </summary>
[GenerateSerializer]
public sealed record FiskalyTransactionSchema(
    [property: Id(0), JsonPropertyName("standard_v1")] FiskalyStandardV1? StandardV1);

/// <summary>
/// Fiskaly Standard V1 receipt data
/// </summary>
[GenerateSerializer]
public sealed record FiskalyStandardV1(
    [property: Id(0), JsonPropertyName("receipt")] FiskalyReceipt Receipt);

/// <summary>
/// Fiskaly receipt structure
/// </summary>
[GenerateSerializer]
public sealed record FiskalyReceipt(
    [property: Id(0), JsonPropertyName("receipt_type")] string ReceiptType,
    [property: Id(1), JsonPropertyName("amounts_per_vat_rate")] List<FiskalyVatAmount> AmountsPerVatRate,
    [property: Id(2), JsonPropertyName("amounts_per_payment_type")] List<FiskalyPaymentAmount> AmountsPerPaymentType);

/// <summary>
/// VAT amount breakdown
/// </summary>
[GenerateSerializer]
public sealed record FiskalyVatAmount(
    [property: Id(0), JsonPropertyName("vat_rate")] string VatRate,
    [property: Id(1), JsonPropertyName("amount")] string Amount);

/// <summary>
/// Payment type amount
/// </summary>
[GenerateSerializer]
public sealed record FiskalyPaymentAmount(
    [property: Id(0), JsonPropertyName("payment_type")] string PaymentType,
    [property: Id(1), JsonPropertyName("amount")] string Amount);

/// <summary>
/// Fiskaly transaction response
/// </summary>
[GenerateSerializer]
public sealed record FiskalyTransactionResponse(
    [property: Id(0), JsonPropertyName("_id")] string Id,
    [property: Id(1), JsonPropertyName("number")] long Number,
    [property: Id(2), JsonPropertyName("state")] string State,
    [property: Id(3), JsonPropertyName("time_start")] long? TimeStart,
    [property: Id(4), JsonPropertyName("time_end")] long? TimeEnd,
    [property: Id(5), JsonPropertyName("log")] FiskalyTransactionLog? Log,
    [property: Id(6), JsonPropertyName("signature")] FiskalySignature? Signature,
    [property: Id(7), JsonPropertyName("qr_code_data")] string? QrCodeData);

/// <summary>
/// Fiskaly transaction log
/// </summary>
[GenerateSerializer]
public sealed record FiskalyTransactionLog(
    [property: Id(0), JsonPropertyName("operation")] string Operation,
    [property: Id(1), JsonPropertyName("timestamp")] long Timestamp,
    [property: Id(2), JsonPropertyName("timestamp_format")] string TimestampFormat);

/// <summary>
/// Fiskaly signature data
/// </summary>
[GenerateSerializer]
public sealed record FiskalySignature(
    [property: Id(0), JsonPropertyName("value")] string Value,
    [property: Id(1), JsonPropertyName("algorithm")] string Algorithm,
    [property: Id(2), JsonPropertyName("counter")] long Counter,
    [property: Id(3), JsonPropertyName("public_key")] string? PublicKey);

/// <summary>
/// Austria RKSV receipt request
/// </summary>
[GenerateSerializer]
public sealed record FiskalyRksvReceiptRequest(
    [property: Id(0), JsonPropertyName("receipt_type")] string ReceiptType,
    [property: Id(1), JsonPropertyName("amounts")] FiskalyRksvAmounts Amounts);

/// <summary>
/// Austria RKSV amounts
/// </summary>
[GenerateSerializer]
public sealed record FiskalyRksvAmounts(
    [property: Id(0), JsonPropertyName("normal")] string? Normal,
    [property: Id(1), JsonPropertyName("reduced1")] string? Reduced1,
    [property: Id(2), JsonPropertyName("reduced2")] string? Reduced2,
    [property: Id(3), JsonPropertyName("zero")] string? Zero,
    [property: Id(4), JsonPropertyName("special")] string? Special);

// ============================================================================
// Fiskaly Client Interface
// ============================================================================

/// <summary>
/// Client for Fiskaly API communication
/// </summary>
public interface IFiskalyClient
{
    /// <summary>
    /// Authenticate with Fiskaly API
    /// </summary>
    Task<FiskalyAuthResponse> AuthenticateAsync(FiskalyConfiguration config, CancellationToken ct = default);

    /// <summary>
    /// Get TSS information
    /// </summary>
    Task<FiskalyTssInfo> GetTssAsync(FiskalyConfiguration config, string accessToken, CancellationToken ct = default);

    /// <summary>
    /// Start a transaction (Germany KassenSichV)
    /// </summary>
    Task<FiskalyTransactionResponse> StartTransactionAsync(
        FiskalyConfiguration config,
        string accessToken,
        string transactionId,
        CancellationToken ct = default);

    /// <summary>
    /// Finish a transaction (Germany KassenSichV)
    /// </summary>
    Task<FiskalyTransactionResponse> FinishTransactionAsync(
        FiskalyConfiguration config,
        string accessToken,
        string transactionId,
        FiskalyReceipt receipt,
        CancellationToken ct = default);

    /// <summary>
    /// Sign a receipt (Austria RKSV)
    /// </summary>
    Task<FiskalyTransactionResponse> SignReceiptAsync(
        FiskalyConfiguration config,
        string accessToken,
        FiskalyRksvReceiptRequest receipt,
        CancellationToken ct = default);
}

/// <summary>
/// HTTP implementation of Fiskaly client
/// </summary>
public sealed class FiskalyHttpClient : IFiskalyClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public FiskalyHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<FiskalyAuthResponse> AuthenticateAsync(FiskalyConfiguration config, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{config.GetBaseUrl()}/auth")
        {
            Content = JsonContent.Create(new { api_key = config.ApiKey, api_secret = config.ApiSecret }, options: _jsonOptions)
        };

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<FiskalyAuthResponse>(_jsonOptions, ct)
            ?? throw new InvalidOperationException("Failed to deserialize auth response");
    }

    public async Task<FiskalyTssInfo> GetTssAsync(FiskalyConfiguration config, string accessToken, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{config.GetBaseUrl()}/tss/{config.TssId}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<FiskalyTssInfo>(_jsonOptions, ct)
            ?? throw new InvalidOperationException("Failed to deserialize TSS info");
    }

    public async Task<FiskalyTransactionResponse> StartTransactionAsync(
        FiskalyConfiguration config,
        string accessToken,
        string transactionId,
        CancellationToken ct = default)
    {
        var url = $"{config.GetBaseUrl()}/tss/{config.TssId}/tx/{transactionId}?tx_revision=1";
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(new FiskalyStartTransactionRequest("ACTIVE", config.ClientId!), options: _jsonOptions)
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<FiskalyTransactionResponse>(_jsonOptions, ct)
            ?? throw new InvalidOperationException("Failed to deserialize transaction response");
    }

    public async Task<FiskalyTransactionResponse> FinishTransactionAsync(
        FiskalyConfiguration config,
        string accessToken,
        string transactionId,
        FiskalyReceipt receipt,
        CancellationToken ct = default)
    {
        var url = $"{config.GetBaseUrl()}/tss/{config.TssId}/tx/{transactionId}?tx_revision=2";
        var finishRequest = new FiskalyFinishTransactionRequest(
            "FINISHED",
            config.ClientId!,
            new FiskalyTransactionSchema(new FiskalyStandardV1(receipt)));

        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(finishRequest, options: _jsonOptions)
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<FiskalyTransactionResponse>(_jsonOptions, ct)
            ?? throw new InvalidOperationException("Failed to deserialize transaction response");
    }

    public async Task<FiskalyTransactionResponse> SignReceiptAsync(
        FiskalyConfiguration config,
        string accessToken,
        FiskalyRksvReceiptRequest receipt,
        CancellationToken ct = default)
    {
        var url = $"{config.GetBaseUrl()}/cash-registers/{config.ClientId}/receipts";
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(receipt, options: _jsonOptions)
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<FiskalyTransactionResponse>(_jsonOptions, ct)
            ?? throw new InvalidOperationException("Failed to deserialize receipt response");
    }
}

// ============================================================================
// Fiskaly Integration Grain Interface
// ============================================================================

/// <summary>
/// Command to configure Fiskaly integration
/// </summary>
[GenerateSerializer]
public sealed record ConfigureFiskalyCommand(
    [property: Id(0)] FiskalyRegion Region,
    [property: Id(1)] FiskalyEnvironment Environment,
    [property: Id(2)] string ApiKey,
    [property: Id(3)] string ApiSecret,
    [property: Id(4)] string? TssId,
    [property: Id(5)] string? ClientId,
    [property: Id(6)] string? OrganizationId);

/// <summary>
/// Fiskaly integration status
/// </summary>
[GenerateSerializer]
public sealed record FiskalyIntegrationSnapshot(
    [property: Id(0)] Guid IntegrationId,
    [property: Id(1)] Guid TenantId,
    [property: Id(2)] bool Enabled,
    [property: Id(3)] FiskalyRegion Region,
    [property: Id(4)] FiskalyEnvironment Environment,
    [property: Id(5)] string? TssId,
    [property: Id(6)] string? TssSerialNumber,
    [property: Id(7)] string? ClientId,
    [property: Id(8)] DateTime? LastSyncAt,
    [property: Id(9)] long TransactionCount,
    [property: Id(10)] string? LastError);

/// <summary>
/// Grain for managing Fiskaly integration per tenant.
/// Subscribes to TSE events and forwards them to Fiskaly.
/// Key: "{orgId}:fiskaly"
/// </summary>
public interface IFiskalyIntegrationGrain : IGrainWithStringKey
{
    /// <summary>
    /// Configure and enable Fiskaly integration
    /// </summary>
    Task<FiskalyIntegrationSnapshot> ConfigureAsync(ConfigureFiskalyCommand command);

    /// <summary>
    /// Disable Fiskaly integration
    /// </summary>
    Task DisableAsync();

    /// <summary>
    /// Get current integration status
    /// </summary>
    Task<FiskalyIntegrationSnapshot> GetSnapshotAsync();

    /// <summary>
    /// Test the Fiskaly connection
    /// </summary>
    Task<bool> TestConnectionAsync();

    /// <summary>
    /// Handle TSE transaction started event
    /// </summary>
    Task HandleTseTransactionStartedAsync(TseTransactionStarted evt);

    /// <summary>
    /// Handle TSE transaction finished event
    /// </summary>
    Task HandleTseTransactionFinishedAsync(TseTransactionFinished evt);

    /// <summary>
    /// Subscribe to TSE events stream
    /// </summary>
    Task SubscribeToTseEventsAsync();
}

// ============================================================================
// Fiskaly Integration State
// ============================================================================

[GenerateSerializer]
public sealed class FiskalyIntegrationState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid IntegrationId { get; set; }
    [Id(2)] public bool Enabled { get; set; }
    [Id(3)] public FiskalyConfiguration? Configuration { get; set; }
    [Id(4)] public string? AccessToken { get; set; }
    [Id(5)] public DateTime? TokenExpiresAt { get; set; }
    [Id(6)] public string? TssSerialNumber { get; set; }
    [Id(7)] public DateTime? LastSyncAt { get; set; }
    [Id(8)] public long TransactionCount { get; set; }
    [Id(9)] public string? LastError { get; set; }
    [Id(10)] public Dictionary<Guid, string> PendingTransactions { get; set; } = new();
    [Id(11)] public int Version { get; set; }
}

// ============================================================================
// Fiskaly Integration Grain Implementation
// ============================================================================

/// <summary>
/// Manages Fiskaly integration for a tenant.
/// Subscribes to TSE events and forwards transactions to Fiskaly API.
/// Configuration-driven: loads settings from FiskalyConfigGrain.
/// </summary>
public sealed class FiskalyIntegrationGrain : Grain, IFiskalyIntegrationGrain, IAsyncObserver<IntegrationEvent>
{
    private readonly IPersistentState<FiskalyIntegrationState> _state;
    private readonly IGrainFactory _grainFactory;
    private readonly IFiskalyClient _fiskalyClient;
    private StreamSubscriptionHandle<IntegrationEvent>? _subscription;
    private IAsyncStream<IntegrationEvent>? _outboundStream;
    private FiskalyConfiguration? _cachedConfig;

    public FiskalyIntegrationGrain(
        [PersistentState("fiskaly", "OrleansStorage")]
        IPersistentState<FiskalyIntegrationState> state,
        IGrainFactory grainFactory,
        IFiskalyClient fiskalyClient)
    {
        _state = state;
        _grainFactory = grainFactory;
        _fiskalyClient = fiskalyClient;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        _state.State.OrgId = orgId;

        // Load configuration from config grain
        await RefreshConfigurationAsync();

        // Auto-subscribe if enabled
        if (_state.State.Enabled && _cachedConfig != null)
        {
            await SubscribeToTseEventsAsync();
        }

        await base.OnActivateAsync(cancellationToken);
    }

    /// <summary>
    /// Refresh configuration from the FiskalyConfigGrain
    /// </summary>
    private async Task RefreshConfigurationAsync()
    {
        var configGrain = _grainFactory.GetGrain<IFiskalyConfigGrain>($"{_state.State.OrgId}:fiskaly:config");
        _cachedConfig = await configGrain.GetFiskalyConfigurationAsync();

        if (_cachedConfig != null)
        {
            _state.State.Enabled = _cachedConfig.Enabled;
            _state.State.Configuration = _cachedConfig;
        }
    }

    /// <summary>
    /// Get current effective configuration, refreshing if needed
    /// </summary>
    private async Task<FiskalyConfiguration?> GetEffectiveConfigurationAsync()
    {
        if (_cachedConfig == null)
        {
            await RefreshConfigurationAsync();
        }
        return _cachedConfig;
    }

    public async Task<FiskalyIntegrationSnapshot> ConfigureAsync(ConfigureFiskalyCommand command)
    {
        // Update config through the config grain for centralized management
        var configGrain = _grainFactory.GetGrain<IFiskalyConfigGrain>($"{_state.State.OrgId}:fiskaly:config");
        await configGrain.UpdateConfigAsync(new UpdateFiskalyTenantConfigCommand(
            Enabled: true,
            Region: command.Region,
            Environment: command.Environment,
            ApiKey: command.ApiKey,
            ApiSecret: command.ApiSecret,
            TssId: command.TssId,
            ClientId: command.ClientId,
            OrganizationId: command.OrganizationId,
            ForwardAllEvents: true,
            RequireExternalSignature: true));

        // Refresh our cached config
        await RefreshConfigurationAsync();

        var config = _cachedConfig!;

        // Test connection and get TSS info
        try
        {
            var auth = await _fiskalyClient.AuthenticateAsync(config);
            _state.State.AccessToken = auth.AccessToken;
            _state.State.TokenExpiresAt = DateTimeOffset.FromUnixTimeSeconds(auth.ExpiresAt).UtcDateTime;

            if (!string.IsNullOrEmpty(config.TssId))
            {
                var tss = await _fiskalyClient.GetTssAsync(config, auth.AccessToken);
                _state.State.TssSerialNumber = tss.SerialNumber;
            }

            _state.State.LastError = null;
        }
        catch (Exception ex)
        {
            _state.State.LastError = ex.Message;
            throw;
        }

        if (_state.State.IntegrationId == Guid.Empty)
        {
            _state.State.IntegrationId = Guid.NewGuid();
        }
        _state.State.Enabled = true;
        _state.State.Configuration = config;
        _state.State.Version++;

        await _state.WriteStateAsync();
        await SubscribeToTseEventsAsync();

        // Publish configuration event
        await PublishEventAsync(new FiskalyIntegrationConfigured(
            IntegrationId: _state.State.IntegrationId,
            TenantId: _state.State.OrgId,
            Region: command.Region.ToString(),
            Environment: command.Environment.ToString(),
            TssId: command.TssId,
            ConfiguredAt: DateTime.UtcNow));

        return CreateSnapshot();
    }

    public async Task DisableAsync()
    {
        // Update config grain to disable
        var configGrain = _grainFactory.GetGrain<IFiskalyConfigGrain>($"{_state.State.OrgId}:fiskaly:config");
        await configGrain.DisableAsync();

        _state.State.Enabled = false;
        _cachedConfig = null;
        _state.State.Version++;
        await _state.WriteStateAsync();

        if (_subscription != null)
        {
            await _subscription.UnsubscribeAsync();
            _subscription = null;
        }

        await PublishEventAsync(new FiskalyIntegrationDisabled(
            IntegrationId: _state.State.IntegrationId,
            TenantId: _state.State.OrgId,
            DisabledAt: DateTime.UtcNow));
    }

    public async Task<FiskalyIntegrationSnapshot> GetSnapshotAsync()
    {
        // Refresh config in case it changed
        await RefreshConfigurationAsync();
        return CreateSnapshot();
    }

    public async Task<bool> TestConnectionAsync()
    {
        var config = await GetEffectiveConfigurationAsync();
        if (config == null)
            return false;

        try
        {
            var auth = await _fiskalyClient.AuthenticateAsync(config);
            _state.State.AccessToken = auth.AccessToken;
            _state.State.TokenExpiresAt = DateTimeOffset.FromUnixTimeSeconds(auth.ExpiresAt).UtcDateTime;
            _state.State.LastError = null;
            await _state.WriteStateAsync();
            return true;
        }
        catch (Exception ex)
        {
            _state.State.LastError = ex.Message;
            await _state.WriteStateAsync();
            return false;
        }
    }

    public async Task SubscribeToTseEventsAsync()
    {
        if (_subscription != null) return;

        try
        {
            var streamProvider = this.GetStreamProvider("Default");
            var stream = streamProvider.GetStream<IntegrationEvent>(
                StreamId.Create("fiscal-tse-events", _state.State.OrgId.ToString()));

            _subscription = await stream.SubscribeAsync(this);
        }
        catch
        {
            // Stream provider may not be configured
        }
    }

    public async Task HandleTseTransactionStartedAsync(TseTransactionStarted evt)
    {
        var config = await GetEffectiveConfigurationAsync();
        if (config == null || !config.Enabled)
            return;

        await EnsureTokenValidAsync();

        try
        {
            var fiskalyTxId = Guid.NewGuid().ToString();
            _state.State.PendingTransactions[evt.TseTransactionId] = fiskalyTxId;

            if (config.Region == FiskalyRegion.Germany)
            {
                var response = await _fiskalyClient.StartTransactionAsync(
                    config,
                    _state.State.AccessToken!,
                    fiskalyTxId);

                await PublishEventAsync(new FiskalyTransactionStarted(
                    TseTransactionId: evt.TseTransactionId,
                    FiskalyTransactionId: response.Id,
                    TenantId: evt.TenantId,
                    TransactionNumber: response.Number,
                    StartedAt: DateTime.UtcNow));
            }

            _state.State.LastError = null;
            await _state.WriteStateAsync();
        }
        catch (Exception ex)
        {
            _state.State.LastError = ex.Message;
            await _state.WriteStateAsync();

            await PublishEventAsync(new FiskalyTransactionFailed(
                TseTransactionId: evt.TseTransactionId,
                TenantId: evt.TenantId,
                ErrorCode: "START_FAILED",
                ErrorMessage: ex.Message,
                FailedAt: DateTime.UtcNow));
        }
    }

    public async Task HandleTseTransactionFinishedAsync(TseTransactionFinished evt)
    {
        var config = await GetEffectiveConfigurationAsync();
        if (config == null || !config.Enabled)
            return;

        await EnsureTokenValidAsync();

        try
        {
            if (!_state.State.PendingTransactions.TryGetValue(evt.TseTransactionId, out var fiskalyTxId))
            {
                // Transaction wasn't started via us - create a new one
                fiskalyTxId = Guid.NewGuid().ToString();
            }

            FiskalyTransactionResponse? response = null;

            switch (config.Region)
            {
                case FiskalyRegion.Germany:
                    var receipt = ParseProcessDataToReceipt(evt.ProcessType, evt.ProcessData);
                    response = await _fiskalyClient.FinishTransactionAsync(
                        config,
                        _state.State.AccessToken!,
                        fiskalyTxId,
                        receipt);
                    break;

                case FiskalyRegion.Austria:
                    var rksvReceipt = ParseProcessDataToRksvReceipt(evt.ProcessType, evt.ProcessData);
                    response = await _fiskalyClient.SignReceiptAsync(
                        config,
                        _state.State.AccessToken!,
                        rksvReceipt);
                    break;

                case FiskalyRegion.Italy:
                    // Italy RT has different flow - implement when needed
                    break;
            }

            _state.State.PendingTransactions.Remove(evt.TseTransactionId);
            _state.State.TransactionCount++;
            _state.State.LastSyncAt = DateTime.UtcNow;
            _state.State.LastError = null;
            await _state.WriteStateAsync();

            if (response != null)
            {
                await PublishEventAsync(new FiskalyTransactionCompleted(
                    TseTransactionId: evt.TseTransactionId,
                    FiskalyTransactionId: response.Id,
                    TenantId: evt.TenantId,
                    TransactionNumber: response.Number,
                    Signature: response.Signature?.Value,
                    SignatureCounter: response.Signature?.Counter ?? 0,
                    QrCodeData: response.QrCodeData,
                    CompletedAt: DateTime.UtcNow));
            }
        }
        catch (Exception ex)
        {
            _state.State.LastError = ex.Message;
            await _state.WriteStateAsync();

            await PublishEventAsync(new FiskalyTransactionFailed(
                TseTransactionId: evt.TseTransactionId,
                TenantId: evt.TenantId,
                ErrorCode: "FINISH_FAILED",
                ErrorMessage: ex.Message,
                FailedAt: DateTime.UtcNow));
        }
    }

    // IAsyncObserver implementation for event stream
    public async Task OnNextAsync(IntegrationEvent item, StreamSequenceToken? token = null)
    {
        switch (item)
        {
            case TseTransactionStarted started:
                await HandleTseTransactionStartedAsync(started);
                break;
            case TseTransactionFinished finished:
                await HandleTseTransactionFinishedAsync(finished);
                break;
        }
    }

    public Task OnCompletedAsync() => Task.CompletedTask;

    public Task OnErrorAsync(Exception ex) => Task.CompletedTask;

    private async Task EnsureTokenValidAsync()
    {
        if (_state.State.AccessToken == null ||
            _state.State.TokenExpiresAt == null ||
            _state.State.TokenExpiresAt < DateTime.UtcNow.AddMinutes(5))
        {
            var auth = await _fiskalyClient.AuthenticateAsync(_state.State.Configuration!);
            _state.State.AccessToken = auth.AccessToken;
            _state.State.TokenExpiresAt = DateTimeOffset.FromUnixTimeSeconds(auth.ExpiresAt).UtcDateTime;
        }
    }

    private static FiskalyReceipt ParseProcessDataToReceipt(string processType, string processData)
    {
        // Parse process data format: GrossAmount^NetAmounts^TaxAmounts^PaymentTypes
        var parts = processData.Split('^');

        var vatAmounts = new List<FiskalyVatAmount>();
        var paymentAmounts = new List<FiskalyPaymentAmount>();

        if (parts.Length > 2 && !string.IsNullOrEmpty(parts[2]))
        {
            foreach (var taxPart in parts[2].Split(','))
            {
                var kv = taxPart.Split(':');
                if (kv.Length == 2)
                {
                    var vatRate = kv[0] switch
                    {
                        "NORMAL" => "NORMAL",
                        "REDUCED" => "REDUCED_1",
                        "NULL" => "NULL",
                        _ => "NORMAL"
                    };
                    vatAmounts.Add(new FiskalyVatAmount(vatRate, kv[1]));
                }
            }
        }

        if (parts.Length > 3 && !string.IsNullOrEmpty(parts[3]))
        {
            foreach (var payPart in parts[3].Split(','))
            {
                var kv = payPart.Split(':');
                if (kv.Length == 2)
                {
                    var payType = kv[0] switch
                    {
                        "CASH" => "CASH",
                        "CARD" => "NON_CASH",
                        _ => "NON_CASH"
                    };
                    paymentAmounts.Add(new FiskalyPaymentAmount(payType, kv[1]));
                }
            }
        }

        var receiptType = processType switch
        {
            "Kassenbeleg" => "RECEIPT",
            "AVTransfer" => "TRANSFER",
            "AVBestellung" => "ORDER",
            _ => "RECEIPT"
        };

        return new FiskalyReceipt(receiptType, vatAmounts, paymentAmounts);
    }

    private static FiskalyRksvReceiptRequest ParseProcessDataToRksvReceipt(string processType, string processData)
    {
        // Parse for Austria RKSV format
        var parts = processData.Split('^');
        string? normal = null, reduced1 = null, reduced2 = null, zero = null;

        if (parts.Length > 2 && !string.IsNullOrEmpty(parts[2]))
        {
            foreach (var taxPart in parts[2].Split(','))
            {
                var kv = taxPart.Split(':');
                if (kv.Length == 2)
                {
                    switch (kv[0])
                    {
                        case "NORMAL": normal = kv[1]; break;
                        case "REDUCED": reduced1 = kv[1]; break;
                        case "REDUCED2": reduced2 = kv[1]; break;
                        case "NULL": zero = kv[1]; break;
                    }
                }
            }
        }

        var receiptType = processType switch
        {
            "Kassenbeleg" => "STANDARD",
            _ => "STANDARD"
        };

        return new FiskalyRksvReceiptRequest(
            receiptType,
            new FiskalyRksvAmounts(normal, reduced1, reduced2, zero, null));
    }

    private FiskalyIntegrationSnapshot CreateSnapshot()
    {
        return new FiskalyIntegrationSnapshot(
            IntegrationId: _state.State.IntegrationId,
            TenantId: _state.State.OrgId,
            Enabled: _state.State.Enabled,
            Region: _state.State.Configuration?.Region ?? FiskalyRegion.Germany,
            Environment: _state.State.Configuration?.Environment ?? FiskalyEnvironment.Test,
            TssId: _state.State.Configuration?.TssId,
            TssSerialNumber: _state.State.TssSerialNumber,
            ClientId: _state.State.Configuration?.ClientId,
            LastSyncAt: _state.State.LastSyncAt,
            TransactionCount: _state.State.TransactionCount,
            LastError: _state.State.LastError);
    }

    private async Task PublishEventAsync(IntegrationEvent evt)
    {
        if (_outboundStream == null)
        {
            try
            {
                var streamProvider = this.GetStreamProvider("Default");
                _outboundStream = streamProvider.GetStream<IntegrationEvent>(
                    StreamId.Create("fiskaly-events", _state.State.OrgId.ToString()));
            }
            catch
            {
                return;
            }
        }

        try
        {
            await _outboundStream.OnNextAsync(evt);
        }
        catch
        {
            // Stream may not be configured
        }
    }
}
