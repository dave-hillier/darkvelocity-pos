using DarkVelocity.PaymentGateway.Api.Dtos;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.PaymentGateway.Api.Controllers;

[ApiController]
[Route("api/v1")]
public class ApiRootController : ControllerBase
{
    [HttpGet]
    public ActionResult<ApiRootDto> GetRoot()
    {
        var root = new ApiRootDto
        {
            Name = "DarkVelocity Payment Gateway",
            Version = "1.0.0",
            Description = "A Stripe-like payment gateway with HAL REST API supporting POS and eCommerce transactions"
        };

        root.AddSelfLink("/api/v1");
        root.AddLink("merchants", "/api/v1/merchants", "Merchant accounts");
        root.AddLink("payment_intents", "/api/v1/payment_intents", "Payment intents");
        root.AddLink("transactions", "/api/v1/transactions", "Transactions");
        root.AddLink("refunds", "/api/v1/refunds", "Refunds");
        root.AddLink("terminals", "/api/v1/terminals", "POS terminals");
        root.AddLink("webhook_endpoints", "/api/v1/webhook_endpoints", "Webhook endpoints");
        root.AddLink("checkout_sessions", "/api/v1/checkout/sessions", "Checkout sessions");

        // Documentation links
        root.AddLink("docs", "/swagger", "API Documentation");
        root.AddLink("docs:payment_intents", "/swagger#/PaymentIntents", "Payment Intents Documentation");
        root.AddLink("docs:terminals", "/swagger#/Terminals", "Terminals Documentation");

        return Ok(root);
    }
}
