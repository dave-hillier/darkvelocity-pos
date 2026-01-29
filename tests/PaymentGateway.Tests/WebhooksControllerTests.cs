using System.Net;
using System.Net.Http.Json;
using DarkVelocity.PaymentGateway.Api.Dtos;
using DarkVelocity.Shared.Contracts.Hal;
using FluentAssertions;

namespace DarkVelocity.PaymentGateway.Tests;

public class WebhooksControllerTests : IClassFixture<PaymentGatewayApiFixture>
{
    private readonly PaymentGatewayApiFixture _fixture;
    private readonly HttpClient _client;

    public WebhooksControllerTests(PaymentGatewayApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task CreateWebhookEndpoint_CreatesEndpointWithSecret()
    {
        var request = new CreateWebhookEndpointRequest(
            Url: $"https://example.com/webhooks/{Guid.NewGuid():N}",
            Description: "Test webhook endpoint",
            EnabledEvents: new List<string> { "payment_intent.succeeded", "refund.created" });

        var response = await _client.PostAsJsonAsync("/api/v1/webhook_endpoints", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var endpoint = await response.Content.ReadFromJsonAsync<WebhookEndpointCreatedDto>();
        endpoint.Should().NotBeNull();
        endpoint!.Url.Should().Contain("example.com");
        endpoint.Description.Should().Be("Test webhook endpoint");
        endpoint.EnabledEvents.Should().Contain("payment_intent.succeeded");
        endpoint.Secret.Should().StartWith("whsec_");
        endpoint.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateWebhookEndpoint_AllEvents_UsesWildcard()
    {
        var request = new CreateWebhookEndpointRequest(
            Url: $"https://example.com/hooks/{Guid.NewGuid():N}");

        var response = await _client.PostAsJsonAsync("/api/v1/webhook_endpoints", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var endpoint = await response.Content.ReadFromJsonAsync<WebhookEndpointCreatedDto>();
        endpoint!.EnabledEvents.Should().Be("*");
    }

    [Fact]
    public async Task CreateWebhookEndpoint_DuplicateUrl_ReturnsBadRequest()
    {
        var url = $"https://example.com/duplicate/{Guid.NewGuid():N}";
        var request = new CreateWebhookEndpointRequest(Url: url);

        await _client.PostAsJsonAsync("/api/v1/webhook_endpoints", request);
        var response = await _client.PostAsJsonAsync("/api/v1/webhook_endpoints", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAll_ReturnsWebhookEndpoints()
    {
        await _client.PostAsJsonAsync(
            "/api/v1/webhook_endpoints",
            new CreateWebhookEndpointRequest(Url: $"https://test.com/{Guid.NewGuid():N}"));

        var response = await _client.GetAsync("/api/v1/webhook_endpoints");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<WebhookEndpointDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetById_ReturnsWebhookEndpoint()
    {
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/webhook_endpoints",
            new CreateWebhookEndpointRequest(Url: $"https://test.com/{Guid.NewGuid():N}"));
        var created = await createResponse.Content.ReadFromJsonAsync<WebhookEndpointDto>();

        var response = await _client.GetAsync($"/api/v1/webhook_endpoints/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var endpoint = await response.Content.ReadFromJsonAsync<WebhookEndpointDto>();
        endpoint!.Id.Should().Be(created.Id);
        endpoint.Links.Should().ContainKey("self");
        endpoint.Links.Should().ContainKey("events");
        endpoint.Links.Should().ContainKey("test");
    }

    [Fact]
    public async Task UpdateWebhookEndpoint_UpdatesUrl()
    {
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/webhook_endpoints",
            new CreateWebhookEndpointRequest(Url: $"https://original.com/{Guid.NewGuid():N}"));
        var created = await createResponse.Content.ReadFromJsonAsync<WebhookEndpointDto>();

        var newUrl = $"https://updated.com/{Guid.NewGuid():N}";
        var updateRequest = new UpdateWebhookEndpointRequest(Url: newUrl);

        var response = await _client.PatchAsJsonAsync(
            $"/api/v1/webhook_endpoints/{created!.Id}",
            updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await response.Content.ReadFromJsonAsync<WebhookEndpointDto>();
        updated!.Url.Should().Be(newUrl);
    }

    [Fact]
    public async Task UpdateWebhookEndpoint_DisableEndpoint()
    {
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/webhook_endpoints",
            new CreateWebhookEndpointRequest(Url: $"https://todisable.com/{Guid.NewGuid():N}"));
        var created = await createResponse.Content.ReadFromJsonAsync<WebhookEndpointDto>();

        var updateRequest = new UpdateWebhookEndpointRequest(IsActive: false);

        var response = await _client.PatchAsJsonAsync(
            $"/api/v1/webhook_endpoints/{created!.Id}",
            updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await response.Content.ReadFromJsonAsync<WebhookEndpointDto>();
        updated!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteWebhookEndpoint_DeletesEndpoint()
    {
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/webhook_endpoints",
            new CreateWebhookEndpointRequest(Url: $"https://todelete.com/{Guid.NewGuid():N}"));
        var created = await createResponse.Content.ReadFromJsonAsync<WebhookEndpointDto>();

        var response = await _client.DeleteAsync($"/api/v1/webhook_endpoints/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync($"/api/v1/webhook_endpoints/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TestWebhook_CreatesTestEvent()
    {
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/webhook_endpoints",
            new CreateWebhookEndpointRequest(Url: $"https://testwebhook.com/{Guid.NewGuid():N}"));
        var created = await createResponse.Content.ReadFromJsonAsync<WebhookEndpointDto>();

        var response = await _client.PostAsync(
            $"/api/v1/webhook_endpoints/{created!.Id}/test",
            null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var eventDto = await response.Content.ReadFromJsonAsync<WebhookEventDto>();
        eventDto!.EventType.Should().Be("test.webhook");
        eventDto.Status.Should().Be("pending");
    }

    [Fact]
    public async Task GetEvents_ReturnsWebhookEvents()
    {
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/webhook_endpoints",
            new CreateWebhookEndpointRequest(Url: $"https://eventtest.com/{Guid.NewGuid():N}"));
        var created = await createResponse.Content.ReadFromJsonAsync<WebhookEndpointDto>();

        // Send a test event
        await _client.PostAsync($"/api/v1/webhook_endpoints/{created!.Id}/test", null);

        var response = await _client.GetAsync($"/api/v1/webhook_endpoints/{created.Id}/events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<WebhookEventDto>>();
        collection!.Embedded.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetEvent_ReturnsEventDetails()
    {
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/webhook_endpoints",
            new CreateWebhookEndpointRequest(Url: $"https://eventdetail.com/{Guid.NewGuid():N}"));
        var created = await createResponse.Content.ReadFromJsonAsync<WebhookEndpointDto>();

        var testResponse = await _client.PostAsync($"/api/v1/webhook_endpoints/{created!.Id}/test", null);
        var testEvent = await testResponse.Content.ReadFromJsonAsync<WebhookEventDto>();

        var response = await _client.GetAsync(
            $"/api/v1/webhook_endpoints/{created.Id}/events/{testEvent!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var eventDetail = await response.Content.ReadFromJsonAsync<WebhookEventDetailDto>();
        eventDetail!.Id.Should().Be(testEvent.Id);
        eventDetail.Payload.Should().NotBeNullOrEmpty();
        eventDetail.Links.Should().ContainKey("retry");
    }

    [Fact]
    public async Task RetryEvent_ResetsEventForRetry()
    {
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/webhook_endpoints",
            new CreateWebhookEndpointRequest(Url: $"https://retrytest.com/{Guid.NewGuid():N}"));
        var created = await createResponse.Content.ReadFromJsonAsync<WebhookEndpointDto>();

        var testResponse = await _client.PostAsync($"/api/v1/webhook_endpoints/{created!.Id}/test", null);
        var testEvent = await testResponse.Content.ReadFromJsonAsync<WebhookEventDto>();

        var response = await _client.PostAsync(
            $"/api/v1/webhook_endpoints/{created.Id}/events/{testEvent!.Id}/retry",
            null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var retried = await response.Content.ReadFromJsonAsync<WebhookEventDto>();
        retried!.Status.Should().Be("pending");
        retried.NextRetryAt.Should().NotBeNull();
    }
}
