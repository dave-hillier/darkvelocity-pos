using System.Text.Json.Serialization;
using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.PaymentGateway.Api.Dtos;

// =====================================================
// MERCHANT DTOs
// =====================================================

public class MerchantDto : HalResource
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public string? BusinessName { get; set; }
    public string? BusinessType { get; set; }
    public string? Country { get; set; }
    public string? DefaultCurrency { get; set; }
    public string? Status { get; set; }
    public bool PayoutsEnabled { get; set; }
    public bool ChargesEnabled { get; set; }
    public string? StatementDescriptor { get; set; }
    public AddressDto? Address { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class AddressDto
{
    public string? Line1 { get; set; }
    public string? Line2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
}

public record CreateMerchantRequest(
    string Name,
    string Email,
    string? BusinessName = null,
    string? BusinessType = null,
    string Country = "US",
    string DefaultCurrency = "USD",
    string? StatementDescriptor = null,
    AddressDto? Address = null,
    Dictionary<string, string>? Metadata = null);

public record UpdateMerchantRequest(
    string? Name = null,
    string? BusinessName = null,
    string? BusinessType = null,
    string? StatementDescriptor = null,
    AddressDto? Address = null,
    Dictionary<string, string>? Metadata = null);

// =====================================================
// API KEY DTOs
// =====================================================

public class ApiKeyDto : HalResource
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public required string Name { get; set; }
    public required string KeyType { get; set; }
    public required string KeyPrefix { get; set; }
    public string? KeyHint { get; set; }
    public bool IsLive { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ApiKeyCreatedDto : ApiKeyDto
{
    // Full key only returned on creation - store it securely!
    public required string Key { get; set; }
}

public record CreateApiKeyRequest(
    string Name,
    string KeyType = "secret", // secret or publishable
    bool IsLive = false,
    DateTime? ExpiresAt = null);

public record RollApiKeyRequest(
    DateTime? ExpiresAt = null);

// =====================================================
// PAYMENT INTENT DTOs
// =====================================================

public class PaymentIntentDto : HalResource
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public long Amount { get; set; }
    public long AmountCapturable { get; set; }
    public long AmountReceived { get; set; }
    public required string Currency { get; set; }
    public required string Status { get; set; }
    public required string CaptureMethod { get; set; }
    public required string ConfirmationMethod { get; set; }
    public required string Channel { get; set; }
    public string? ClientSecret { get; set; }
    public Guid? PaymentMethodId { get; set; }
    public string? PaymentMethodType { get; set; }
    public CardDetailsDto? Card { get; set; }
    public Guid? TerminalId { get; set; }
    public string? Description { get; set; }
    public string? StatementDescriptor { get; set; }
    public string? ReceiptEmail { get; set; }
    public string? ExternalOrderId { get; set; }
    public string? ExternalCustomerId { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? CanceledAt { get; set; }
    public DateTime? SucceededAt { get; set; }
    public PaymentIntentErrorDto? LastError { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CardDetailsDto
{
    public string? Brand { get; set; }
    public string? Last4 { get; set; }
    public string? ExpMonth { get; set; }
    public string? ExpYear { get; set; }
    public string? Funding { get; set; }
}

public class PaymentIntentErrorDto
{
    public string? Code { get; set; }
    public string? Message { get; set; }
}

public record CreatePaymentIntentRequest(
    long Amount,
    string Currency = "usd",
    string CaptureMethod = "automatic",
    string ConfirmationMethod = "automatic",
    string Channel = "ecommerce",
    Guid? TerminalId = null,
    string? Description = null,
    string? StatementDescriptor = null,
    string? StatementDescriptorSuffix = null,
    string? ReceiptEmail = null,
    string? ExternalOrderId = null,
    string? ExternalCustomerId = null,
    Dictionary<string, string>? Metadata = null);

public record ConfirmPaymentIntentRequest(
    string? PaymentMethodType = "card",
    CardPaymentMethodRequest? Card = null,
    CardPresentPaymentMethodRequest? CardPresent = null);

public record CardPaymentMethodRequest(
    string Number,
    string ExpMonth,
    string ExpYear,
    string Cvc);

public record CardPresentPaymentMethodRequest(
    // For simulated terminal testing
    string? TestCardNumber = null);

public record CapturePaymentIntentRequest(
    long? AmountToCapture = null);

public record CancelPaymentIntentRequest(
    string? CancellationReason = null);

public record UpdatePaymentIntentRequest(
    string? Description = null,
    string? ReceiptEmail = null,
    Dictionary<string, string>? Metadata = null);

// =====================================================
// TRANSACTION DTOs
// =====================================================

public class TransactionDto : HalResource
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public Guid PaymentIntentId { get; set; }
    public required string Type { get; set; }
    public long Amount { get; set; }
    public required string Currency { get; set; }
    public required string Status { get; set; }
    public CardDetailsDto? Card { get; set; }
    public string? AuthorizationCode { get; set; }
    public string? NetworkTransactionId { get; set; }
    public string? RiskLevel { get; set; }
    public int? RiskScore { get; set; }
    public TransactionFailureDto? Failure { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TransactionFailureDto
{
    public string? Code { get; set; }
    public string? Message { get; set; }
    public string? DeclineCode { get; set; }
}

// =====================================================
// REFUND DTOs
// =====================================================

public class RefundDto : HalResource
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public Guid PaymentIntentId { get; set; }
    public long Amount { get; set; }
    public required string Currency { get; set; }
    public required string Status { get; set; }
    public string? Reason { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? FailureReason { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SucceededAt { get; set; }
}

public record CreateRefundRequest(
    Guid PaymentIntentId,
    long? Amount = null, // null = full refund
    string? Reason = null,
    Dictionary<string, string>? Metadata = null);

// =====================================================
// TERMINAL DTOs
// =====================================================

public class TerminalDto : HalResource
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public required string Label { get; set; }
    public required string DeviceType { get; set; }
    public string? SerialNumber { get; set; }
    public string? DeviceSwVersion { get; set; }
    public string? LocationName { get; set; }
    public string? LocationAddress { get; set; }
    public Guid? ExternalLocationId { get; set; }
    public bool IsRegistered { get; set; }
    public required string Status { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TerminalRegistrationDto : TerminalDto
{
    // Registration code only returned on creation
    public string? RegistrationCode { get; set; }
    public DateTime? RegistrationCodeExpiresAt { get; set; }
}

public record CreateTerminalRequest(
    string Label,
    string DeviceType = "simulated",
    string? LocationName = null,
    string? LocationAddress = null,
    Guid? ExternalLocationId = null,
    Dictionary<string, string>? Metadata = null);

public record UpdateTerminalRequest(
    string? Label = null,
    string? LocationName = null,
    string? LocationAddress = null,
    Guid? ExternalLocationId = null,
    Dictionary<string, string>? Metadata = null);

public record RegisterTerminalRequest(
    string RegistrationCode);

// Terminal reader action - collect payment
public record TerminalCollectPaymentRequest(
    Guid PaymentIntentId);

public class TerminalReaderActionDto : HalResource
{
    public Guid TerminalId { get; set; }
    public Guid PaymentIntentId { get; set; }
    public required string ActionType { get; set; } // collect_payment_method, process_payment
    public required string Status { get; set; } // in_progress, succeeded, failed
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
}

// =====================================================
// WEBHOOK ENDPOINT DTOs
// =====================================================

public class WebhookEndpointDto : HalResource
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public required string Url { get; set; }
    public string? Description { get; set; }
    public required string EnabledEvents { get; set; }
    public bool IsActive { get; set; }
    public string? ApiVersion { get; set; }
    public int ConsecutiveFailures { get; set; }
    public DateTime? DisabledAt { get; set; }
    public string? DisabledReason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class WebhookEndpointCreatedDto : WebhookEndpointDto
{
    // Secret only returned on creation
    public required string Secret { get; set; }
}

public record CreateWebhookEndpointRequest(
    string Url,
    string? Description = null,
    List<string>? EnabledEvents = null, // null = all events (*)
    string? ApiVersion = null,
    Dictionary<string, string>? Metadata = null);

public record UpdateWebhookEndpointRequest(
    string? Url = null,
    string? Description = null,
    List<string>? EnabledEvents = null,
    bool? IsActive = null,
    Dictionary<string, string>? Metadata = null);

// =====================================================
// WEBHOOK EVENT DTOs
// =====================================================

public class WebhookEventDto : HalResource
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public Guid WebhookEndpointId { get; set; }
    public required string EventType { get; set; }
    public required string ObjectType { get; set; }
    public Guid ObjectId { get; set; }
    public required string Status { get; set; }
    public int DeliveryAttempts { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public int? ResponseStatusCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class WebhookEventDetailDto : WebhookEventDto
{
    // Full payload included in detail view
    public string? Payload { get; set; }
    public string? ResponseBody { get; set; }
}

// Webhook payload structure
public class WebhookPayload
{
    public Guid Id { get; set; }
    [JsonPropertyName("type")]
    public required string EventType { get; set; }
    [JsonPropertyName("created")]
    public long Created { get; set; }
    [JsonPropertyName("api_version")]
    public string? ApiVersion { get; set; }
    [JsonPropertyName("data")]
    public WebhookPayloadData? Data { get; set; }
}

public class WebhookPayloadData
{
    [JsonPropertyName("object")]
    public object? Object { get; set; }
}

// =====================================================
// CHECKOUT SESSION DTOs (eCommerce)
// =====================================================

public class CheckoutSessionDto : HalResource
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public Guid PaymentIntentId { get; set; }
    public required string Url { get; set; }
    public required string Status { get; set; } // open, complete, expired
    public long AmountTotal { get; set; }
    public required string Currency { get; set; }
    public string? SuccessUrl { get; set; }
    public string? CancelUrl { get; set; }
    public string? CustomerEmail { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public record CreateCheckoutSessionRequest(
    long Amount,
    string Currency = "usd",
    string? SuccessUrl = null,
    string? CancelUrl = null,
    string? CustomerEmail = null,
    string? ExternalOrderId = null,
    string? ExternalCustomerId = null,
    Dictionary<string, string>? Metadata = null);

// =====================================================
// API ROOT DTOs
// =====================================================

public class ApiRootDto : HalResource
{
    public required string Name { get; set; }
    public required string Version { get; set; }
    public required string Description { get; set; }
}

// =====================================================
// ERROR DTOs
// =====================================================

public class ApiErrorDto
{
    [JsonPropertyName("error")]
    public required ApiErrorDetailDto Error { get; set; }
}

public class ApiErrorDetailDto
{
    public required string Type { get; set; } // api_error, card_error, invalid_request_error
    public required string Code { get; set; }
    public required string Message { get; set; }
    public string? Param { get; set; }
    public string? DeclineCode { get; set; }
}
