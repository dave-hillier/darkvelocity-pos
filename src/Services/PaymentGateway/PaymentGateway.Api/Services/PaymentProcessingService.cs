using DarkVelocity.PaymentGateway.Api.Data;
using DarkVelocity.PaymentGateway.Api.Dtos;
using DarkVelocity.PaymentGateway.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.PaymentGateway.Api.Services;

public class PaymentProcessingService
{
    private readonly PaymentGatewayDbContext _context;
    private readonly WebhookService _webhookService;

    // Simulated test card numbers
    private static readonly Dictionary<string, (bool Success, string? DeclineCode)> TestCards = new()
    {
        { "4242424242424242", (true, null) },           // Visa - success
        { "5555555555554444", (true, null) },           // Mastercard - success
        { "378282246310005", (true, null) },            // Amex - success
        { "4000000000000002", (false, "card_declined") }, // Generic decline
        { "4000000000009995", (false, "insufficient_funds") },
        { "4000000000009987", (false, "lost_card") },
        { "4000000000009979", (false, "stolen_card") },
        { "4000000000000069", (false, "expired_card") },
        { "4000000000000127", (false, "incorrect_cvc") },
        { "4000000000000119", (false, "processing_error") },
    };

    public PaymentProcessingService(PaymentGatewayDbContext context, WebhookService webhookService)
    {
        _context = context;
        _webhookService = webhookService;
    }

    public async Task<(bool Success, Transaction Transaction)> ProcessCardPayment(
        PaymentIntent paymentIntent,
        CardPaymentMethodRequest card)
    {
        // Simulate card processing
        var cardNumber = card.Number.Replace(" ", "");
        var (success, declineCode) = SimulateCardAuthorization(cardNumber);

        var transaction = new Transaction
        {
            MerchantId = paymentIntent.MerchantId,
            PaymentIntentId = paymentIntent.Id,
            Type = paymentIntent.CaptureMethod == "manual" ? "authorization" : "charge",
            Amount = paymentIntent.Amount,
            Currency = paymentIntent.Currency,
            Status = success ? "succeeded" : "failed",
            CardBrand = DetectCardBrand(cardNumber),
            CardLast4 = cardNumber.Substring(cardNumber.Length - 4),
            CardFunding = "credit", // Simplified
            AuthorizationCode = success ? GenerateAuthCode() : null,
            NetworkTransactionId = success ? Guid.NewGuid().ToString("N").Substring(0, 16) : null,
            ProcessorResponseCode = success ? "00" : "05",
            ProcessorResponseText = success ? "Approved" : "Declined",
            RiskLevel = "normal",
            RiskScore = new Random().Next(0, 30),
            DeclineCode = declineCode,
            FailureCode = success ? null : "card_declined",
            FailureMessage = success ? null : GetDeclineMessage(declineCode)
        };

        _context.Transactions.Add(transaction);

        // Update payment intent
        paymentIntent.PaymentMethodType = "card";
        paymentIntent.CardBrand = transaction.CardBrand;
        paymentIntent.CardLast4 = transaction.CardLast4;
        paymentIntent.CardExpMonth = card.ExpMonth;
        paymentIntent.CardExpYear = card.ExpYear;
        paymentIntent.CardFunding = transaction.CardFunding;

        if (success)
        {
            if (paymentIntent.CaptureMethod == "manual")
            {
                paymentIntent.Status = "requires_capture";
                paymentIntent.AmountCapturable = paymentIntent.Amount;
            }
            else
            {
                paymentIntent.Status = "succeeded";
                paymentIntent.AmountReceived = paymentIntent.Amount;
                paymentIntent.SucceededAt = DateTime.UtcNow;
            }
        }
        else
        {
            paymentIntent.Status = "requires_payment_method";
            paymentIntent.LastErrorCode = transaction.FailureCode;
            paymentIntent.LastErrorMessage = transaction.FailureMessage;
        }

        paymentIntent.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Send webhook
        if (success && paymentIntent.Status == "succeeded")
        {
            await _webhookService.SendWebhookAsync(paymentIntent.MerchantId, "payment_intent.succeeded", "payment_intent", paymentIntent.Id);
        }

        return (success, transaction);
    }

    public async Task<(bool Success, Transaction Transaction)> ProcessCardPresentPayment(
        PaymentIntent paymentIntent,
        Terminal terminal)
    {
        // Simulate card-present (POS terminal) payment
        // In reality, this would communicate with the physical terminal
        var success = true;

        var transaction = new Transaction
        {
            MerchantId = paymentIntent.MerchantId,
            PaymentIntentId = paymentIntent.Id,
            Type = paymentIntent.CaptureMethod == "manual" ? "authorization" : "charge",
            Amount = paymentIntent.Amount,
            Currency = paymentIntent.Currency,
            Status = "succeeded",
            CardBrand = "visa", // Simulated
            CardLast4 = "4242", // Simulated
            CardFunding = "credit",
            AuthorizationCode = GenerateAuthCode(),
            NetworkTransactionId = Guid.NewGuid().ToString("N").Substring(0, 16),
            ProcessorResponseCode = "00",
            ProcessorResponseText = "Approved",
            RiskLevel = "normal",
            RiskScore = 5 // Lower risk for card-present
        };

        _context.Transactions.Add(transaction);

        // Update payment intent
        paymentIntent.PaymentMethodType = "card_present";
        paymentIntent.CardBrand = transaction.CardBrand;
        paymentIntent.CardLast4 = transaction.CardLast4;
        paymentIntent.CardFunding = transaction.CardFunding;

        if (paymentIntent.CaptureMethod == "manual")
        {
            paymentIntent.Status = "requires_capture";
            paymentIntent.AmountCapturable = paymentIntent.Amount;
        }
        else
        {
            paymentIntent.Status = "succeeded";
            paymentIntent.AmountReceived = paymentIntent.Amount;
            paymentIntent.SucceededAt = DateTime.UtcNow;
        }

        paymentIntent.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Update terminal last seen
        terminal.LastSeenAt = DateTime.UtcNow;
        terminal.Status = "online";
        await _context.SaveChangesAsync();

        // Send webhook
        if (paymentIntent.Status == "succeeded")
        {
            await _webhookService.SendWebhookAsync(paymentIntent.MerchantId, "payment_intent.succeeded", "payment_intent", paymentIntent.Id);
        }

        return (success, transaction);
    }

    public async Task<(bool Success, Transaction? Transaction)> CapturePayment(
        PaymentIntent paymentIntent,
        long? amountToCapture = null)
    {
        var captureAmount = amountToCapture ?? paymentIntent.AmountCapturable;

        if (captureAmount <= 0 || captureAmount > paymentIntent.AmountCapturable)
        {
            return (false, null);
        }

        var transaction = new Transaction
        {
            MerchantId = paymentIntent.MerchantId,
            PaymentIntentId = paymentIntent.Id,
            Type = "capture",
            Amount = captureAmount,
            Currency = paymentIntent.Currency,
            Status = "succeeded",
            CardBrand = paymentIntent.CardBrand,
            CardLast4 = paymentIntent.CardLast4,
            CardFunding = paymentIntent.CardFunding,
            AuthorizationCode = GenerateAuthCode(),
            NetworkTransactionId = Guid.NewGuid().ToString("N").Substring(0, 16),
            ProcessorResponseCode = "00",
            ProcessorResponseText = "Captured"
        };

        _context.Transactions.Add(transaction);

        paymentIntent.AmountReceived += captureAmount;
        paymentIntent.AmountCapturable -= captureAmount;

        if (paymentIntent.AmountCapturable == 0)
        {
            paymentIntent.Status = "succeeded";
            paymentIntent.SucceededAt = DateTime.UtcNow;
        }

        paymentIntent.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Send webhook
        await _webhookService.SendWebhookAsync(paymentIntent.MerchantId, "payment_intent.succeeded", "payment_intent", paymentIntent.Id);

        return (true, transaction);
    }

    private (bool Success, string? DeclineCode) SimulateCardAuthorization(string cardNumber)
    {
        if (TestCards.TryGetValue(cardNumber, out var result))
        {
            return result;
        }

        // Default to success for unknown cards in test mode
        return (true, null);
    }

    private static string DetectCardBrand(string cardNumber)
    {
        if (cardNumber.StartsWith("4"))
            return "visa";
        if (cardNumber.StartsWith("5") || cardNumber.StartsWith("2"))
            return "mastercard";
        if (cardNumber.StartsWith("34") || cardNumber.StartsWith("37"))
            return "amex";
        if (cardNumber.StartsWith("6"))
            return "discover";
        return "unknown";
    }

    private static string GenerateAuthCode()
    {
        return new Random().Next(100000, 999999).ToString();
    }

    private static string? GetDeclineMessage(string? declineCode)
    {
        return declineCode switch
        {
            "card_declined" => "The card was declined.",
            "insufficient_funds" => "The card has insufficient funds.",
            "lost_card" => "The card has been reported lost.",
            "stolen_card" => "The card has been reported stolen.",
            "expired_card" => "The card has expired.",
            "incorrect_cvc" => "The CVC number is incorrect.",
            "processing_error" => "An error occurred while processing the card.",
            _ => "The card was declined."
        };
    }
}
