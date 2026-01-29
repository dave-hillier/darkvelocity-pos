using System.Net;
using System.Net.Http.Json;
using DarkVelocity.PaymentGateway.Api.Dtos;
using FluentAssertions;

namespace DarkVelocity.PaymentGateway.Tests;

public class MerchantsControllerTests : IClassFixture<PaymentGatewayApiFixture>
{
    private readonly PaymentGatewayApiFixture _fixture;
    private readonly HttpClient _client;
    private readonly HttpClient _unauthenticatedClient;

    public MerchantsControllerTests(PaymentGatewayApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
        _unauthenticatedClient = fixture.UnauthenticatedClient;
    }

    [Fact]
    public async Task CreateMerchant_CreatesMerchantWithApiKeys()
    {
        var request = new CreateMerchantRequest(
            Name: "New Merchant",
            Email: $"new{Guid.NewGuid():N}@example.com",
            BusinessName: "New Business",
            BusinessType: "individual",
            Country: "US",
            DefaultCurrency: "USD");

        var response = await _unauthenticatedClient.PostAsJsonAsync("/api/v1/merchants", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("merchant");
        content.Should().Contain("api_keys");
        content.Should().Contain("test_secret_key");
        content.Should().Contain("sk_test_");
    }

    [Fact]
    public async Task CreateMerchant_DuplicateEmail_ReturnsBadRequest()
    {
        var request = new CreateMerchantRequest(
            Name: "Duplicate Merchant",
            Email: "test@example.com", // Already exists
            BusinessName: "Dupe Business");

        var response = await _unauthenticatedClient.PostAsJsonAsync("/api/v1/merchants", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetMerchant_ReturnsOwnMerchant()
    {
        var response = await _client.GetAsync($"/api/v1/merchants/{_fixture.TestMerchantId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var merchant = await response.Content.ReadFromJsonAsync<MerchantDto>();
        merchant.Should().NotBeNull();
        merchant!.Id.Should().Be(_fixture.TestMerchantId);
        merchant.Name.Should().Be("Test Merchant");
        merchant.Links.Should().ContainKey("self");
        merchant.Links.Should().ContainKey("api_keys");
    }

    [Fact]
    public async Task GetMerchant_OtherMerchant_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/v1/merchants/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMerchant_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await _unauthenticatedClient.GetAsync($"/api/v1/merchants/{_fixture.TestMerchantId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateMerchant_UpdatesFields()
    {
        var request = new UpdateMerchantRequest(
            BusinessName: "Updated Business Name");

        var response = await _client.PatchAsJsonAsync($"/api/v1/merchants/{_fixture.TestMerchantId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var merchant = await response.Content.ReadFromJsonAsync<MerchantDto>();
        merchant!.BusinessName.Should().Be("Updated Business Name");
    }

    [Fact]
    public async Task GetApiKeys_ReturnsApiKeys()
    {
        var response = await _client.GetAsync($"/api/v1/merchants/{_fixture.TestMerchantId}/api_keys");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("items");
        content.Should().Contain("sk_test_");
        content.Should().Contain("pk_test_");
    }

    [Fact]
    public async Task CreateApiKey_CreatesNewKey()
    {
        var request = new CreateApiKeyRequest(
            Name: "New Test Key",
            KeyType: "secret",
            IsLive: false);

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/merchants/{_fixture.TestMerchantId}/api_keys",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var apiKey = await response.Content.ReadFromJsonAsync<ApiKeyCreatedDto>();
        apiKey.Should().NotBeNull();
        apiKey!.Key.Should().StartWith("sk_test_");
        apiKey.Name.Should().Be("New Test Key");
    }

    [Fact]
    public async Task RevokeApiKey_RevokesKey()
    {
        // First create a new key
        var createRequest = new CreateApiKeyRequest(
            Name: "Key to Revoke",
            KeyType: "secret",
            IsLive: false);

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/v1/merchants/{_fixture.TestMerchantId}/api_keys",
            createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiKeyCreatedDto>();

        // Revoke it
        var response = await _client.PostAsync(
            $"/api/v1/merchants/{_fixture.TestMerchantId}/api_keys/{created!.Id}/revoke",
            null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify the key no longer works
        var revokedClient = _fixture.CreateAuthenticatedClient(created.Key);
        var testResponse = await revokedClient.GetAsync($"/api/v1/merchants/{_fixture.TestMerchantId}");
        testResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
