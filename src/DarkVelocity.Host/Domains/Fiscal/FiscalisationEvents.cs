namespace DarkVelocity.Host.Events;

// ============================================================================
// TSE Operation Events
// These events represent TSE-like operations that can be optionally mapped
// to an external TSE device (e.g., Swissbit, Fiskaly, etc.)
// ============================================================================

/// <summary>
/// Published when a TSE transaction is started.
/// This event is generated internally and can be forwarded to an external TSE.
/// </summary>
[GenerateSerializer]
public sealed record TseTransactionStarted(
    [property: Id(0)] Guid TseTransactionId,
    [property: Id(1)] Guid DeviceId,
    [property: Id(2)] Guid LocationId,
    [property: Id(3)] Guid TenantId,
    [property: Id(4)] long TransactionNumber,
    [property: Id(5)] string ProcessType,
    [property: Id(6)] string ProcessData,
    [property: Id(7)] DateTime StartTime,
    [property: Id(8)] string? ClientId
) : IntegrationEvent
{
    public override string EventType => "tse.transaction.started";
}

/// <summary>
/// Published when transaction data is updated in the TSE.
/// Represents line items, payments, or other transaction updates.
/// </summary>
public sealed record TseTransactionUpdated(
    Guid TseTransactionId,
    Guid DeviceId,
    Guid LocationId,
    Guid TenantId,
    long TransactionNumber,
    string ProcessData,
    DateTime UpdateTime
) : IntegrationEvent
{
    public override string EventType => "tse.transaction.updated";
}

/// <summary>
/// Published when a TSE transaction is finished and signed.
/// Contains the signature and all data required for the QR code.
/// </summary>
[GenerateSerializer]
public sealed record TseTransactionFinished(
    [property: Id(0)] Guid TseTransactionId,
    [property: Id(1)] Guid DeviceId,
    [property: Id(2)] Guid LocationId,
    [property: Id(3)] Guid TenantId,
    [property: Id(4)] long TransactionNumber,
    [property: Id(5)] long SignatureCounter,
    [property: Id(6)] string Signature,
    [property: Id(7)] string SignatureAlgorithm,
    [property: Id(8)] string PublicKeyBase64,
    [property: Id(9)] DateTime StartTime,
    [property: Id(10)] DateTime EndTime,
    [property: Id(11)] string ProcessType,
    [property: Id(12)] string ProcessData,
    [property: Id(13)] string CertificateSerial,
    [property: Id(14)] string TimeFormat,
    [property: Id(15)] string QrCodeData
) : IntegrationEvent
{
    public override string EventType => "tse.transaction.finished";
}

/// <summary>
/// Published when a TSE transaction fails to complete.
/// </summary>
public sealed record TseTransactionFailed(
    Guid TseTransactionId,
    Guid DeviceId,
    Guid LocationId,
    Guid TenantId,
    long TransactionNumber,
    string ErrorCode,
    string ErrorMessage,
    DateTime FailedAt
) : IntegrationEvent
{
    public override string EventType => "tse.transaction.failed";
}

/// <summary>
/// Published when an external TSE response is received.
/// This event bridges internal TSE operations with external TSE devices.
/// </summary>
public sealed record ExternalTseResponseReceived(
    Guid TseTransactionId,
    Guid DeviceId,
    Guid LocationId,
    Guid TenantId,
    string ExternalTransactionId,
    string ExternalSignature,
    string ExternalCertificateSerial,
    long ExternalSignatureCounter,
    DateTime ExternalTimestamp,
    string RawResponse
) : IntegrationEvent
{
    public override string EventType => "tse.external.response_received";
}

/// <summary>
/// Published when TSE self-test is performed.
/// </summary>
public sealed record TseSelfTestPerformed(
    Guid DeviceId,
    Guid LocationId,
    Guid TenantId,
    bool Passed,
    string? ErrorMessage,
    DateTime PerformedAt
) : IntegrationEvent
{
    public override string EventType => "tse.self_test.performed";
}

// ============================================================================
// Existing Fiscal Integration Events
// ============================================================================

/// <summary>
/// Published when a transaction is successfully signed by a TSE device
/// </summary>
public sealed record TransactionSigned(
    Guid TransactionId,
    Guid DeviceId,
    Guid LocationId,
    Guid TenantId,
    long TransactionNumber,
    long SignatureCounter,
    string? Signature,
    decimal GrossAmount,
    DateTime SignedAt
) : IntegrationEvent
{
    public override string EventType => "fiscalisation.transaction.signed";
}

/// <summary>
/// Published when transaction signing fails
/// </summary>
public sealed record TransactionSigningFailed(
    Guid TransactionId,
    Guid DeviceId,
    Guid LocationId,
    Guid TenantId,
    string? ErrorCode,
    string? ErrorMessage,
    bool WillRetry
) : IntegrationEvent
{
    public override string EventType => "fiscalisation.transaction.signing_failed";
}

/// <summary>
/// Published when a fiscal device status changes
/// </summary>
public sealed record FiscalDeviceHealthChanged(
    Guid DeviceId,
    Guid LocationId,
    Guid TenantId,
    string OldStatus,
    string NewStatus,
    string? Reason
) : IntegrationEvent
{
    public override string EventType => "fiscalisation.device.health_changed";
}

/// <summary>
/// Published when a DSFinV-K export is generated
/// </summary>
public sealed record ExportGenerated(
    Guid ExportId,
    Guid LocationId,
    Guid TenantId,
    DateTime StartDate,
    DateTime EndDate,
    int TransactionCount,
    string? FileUrl
) : IntegrationEvent
{
    public override string EventType => "fiscalisation.export.generated";
}

/// <summary>
/// Published when a new fiscal device is registered
/// </summary>
public sealed record FiscalDeviceRegistered(
    Guid DeviceId,
    Guid LocationId,
    Guid TenantId,
    string DeviceType,
    string SerialNumber
) : IntegrationEvent
{
    public override string EventType => "fiscalisation.device.registered";
}

/// <summary>
/// Published when a fiscal device is decommissioned
/// </summary>
public sealed record FiscalDeviceDecommissioned(
    Guid DeviceId,
    Guid LocationId,
    Guid TenantId,
    string Reason
) : IntegrationEvent
{
    public override string EventType => "fiscalisation.device.decommissioned";
}

// ============================================================================
// Fiskaly Integration Events
// Events for Fiskaly cloud TSE integration (Germany, Austria, Italy)
// ============================================================================

/// <summary>
/// Published when Fiskaly integration is configured for a tenant
/// </summary>
public sealed record FiskalyIntegrationConfigured(
    Guid IntegrationId,
    Guid TenantId,
    string Region,
    string Environment,
    string? TssId,
    DateTime ConfiguredAt
) : IntegrationEvent
{
    public override string EventType => "fiskaly.integration.configured";
}

/// <summary>
/// Published when Fiskaly integration is disabled
/// </summary>
public sealed record FiskalyIntegrationDisabled(
    Guid IntegrationId,
    Guid TenantId,
    DateTime DisabledAt
) : IntegrationEvent
{
    public override string EventType => "fiskaly.integration.disabled";
}

/// <summary>
/// Published when a transaction is started in Fiskaly
/// </summary>
public sealed record FiskalyTransactionStarted(
    Guid TseTransactionId,
    string FiskalyTransactionId,
    Guid TenantId,
    long TransactionNumber,
    DateTime StartedAt
) : IntegrationEvent
{
    public override string EventType => "fiskaly.transaction.started";
}

/// <summary>
/// Published when a transaction is completed and signed in Fiskaly
/// </summary>
public sealed record FiskalyTransactionCompleted(
    Guid TseTransactionId,
    string FiskalyTransactionId,
    Guid TenantId,
    long TransactionNumber,
    string? Signature,
    long SignatureCounter,
    string? QrCodeData,
    DateTime CompletedAt
) : IntegrationEvent
{
    public override string EventType => "fiskaly.transaction.completed";
}

/// <summary>
/// Published when a Fiskaly transaction fails
/// </summary>
public sealed record FiskalyTransactionFailed(
    Guid TseTransactionId,
    Guid TenantId,
    string ErrorCode,
    string ErrorMessage,
    DateTime FailedAt
) : IntegrationEvent
{
    public override string EventType => "fiskaly.transaction.failed";
}

/// <summary>
/// Published when Fiskaly TSS status changes
/// </summary>
public sealed record FiskalyTssStatusChanged(
    Guid IntegrationId,
    Guid TenantId,
    string TssId,
    string OldStatus,
    string NewStatus,
    DateTime ChangedAt
) : IntegrationEvent
{
    public override string EventType => "fiskaly.tss.status_changed";
}

/// <summary>
/// Published when a batch of transactions is synced to Fiskaly
/// </summary>
public sealed record FiskalySyncCompleted(
    Guid IntegrationId,
    Guid TenantId,
    int TransactionCount,
    int SuccessCount,
    int FailureCount,
    DateTime SyncedAt
) : IntegrationEvent
{
    public override string EventType => "fiskaly.sync.completed";
}
