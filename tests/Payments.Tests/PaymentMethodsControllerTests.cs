using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Payments.Api.Dtos;
using DarkVelocity.Shared.Contracts.Hal;
using FluentAssertions;

namespace DarkVelocity.Payments.Tests;

public class PaymentMethodsControllerTests : IClassFixture<PaymentsApiFixture>
{
    private readonly PaymentsApiFixture _fixture;
    private readonly HttpClient _client;

    public PaymentMethodsControllerTests(PaymentsApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAll_ReturnsPaymentMethods()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/payment-methods");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<PaymentMethodDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_FiltersByActive()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/payment-methods?activeOnly=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<PaymentMethodDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().OnlyContain(pm => pm.IsActive);
    }

    [Fact]
    public async Task GetById_ReturnsPaymentMethod()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/payment-methods/{_fixture.TestCashPaymentMethodId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var method = await response.Content.ReadFromJsonAsync<PaymentMethodDto>();
        method.Should().NotBeNull();
        method!.Id.Should().Be(_fixture.TestCashPaymentMethodId);
        method.Name.Should().Be("Cash");
        method.MethodType.Should().Be("cash");
    }

    [Fact]
    public async Task GetById_NotFound_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/payment-methods/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_CreatesPaymentMethod()
    {
        var request = new CreatePaymentMethodRequest(
            Name: "Gift Card",
            MethodType: "voucher",
            RequiresTip: false,
            OpensDrawer: false,
            DisplayOrder: 5);

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payment-methods",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var method = await response.Content.ReadFromJsonAsync<PaymentMethodDto>();
        method.Should().NotBeNull();
        method!.Name.Should().Be("Gift Card");
        method.MethodType.Should().Be("voucher");
        method.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Create_DuplicateName_ReturnsConflict()
    {
        var request = new CreatePaymentMethodRequest(
            Name: "Cash",  // Already exists
            MethodType: "cash");

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payment-methods",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_UpdatesPaymentMethod()
    {
        // First create a new method
        var createRequest = new CreatePaymentMethodRequest(
            Name: $"Test-{Guid.NewGuid():N}",
            MethodType: "cash",
            DisplayOrder: 10);

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payment-methods",
            createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<PaymentMethodDto>();

        // Update it
        var updateRequest = new UpdatePaymentMethodRequest(
            Name: "Updated Method",
            OpensDrawer: false,
            DisplayOrder: 99);

        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payment-methods/{created!.Id}",
            updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await response.Content.ReadFromJsonAsync<PaymentMethodDto>();
        updated!.Name.Should().Be("Updated Method");
        updated.OpensDrawer.Should().BeFalse();
        updated.DisplayOrder.Should().Be(99);
    }

    [Fact]
    public async Task Delete_DeactivatesPaymentMethodWithPayments()
    {
        // The cash method has payments attached, so it should be deactivated not deleted
        // First we need to verify this works
        var deleteResponse = await _client.DeleteAsync(
            $"/api/locations/{_fixture.TestLocationId}/payment-methods/{_fixture.TestCashPaymentMethodId}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's deactivated, not deleted
        var getResponse = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/payment-methods/{_fixture.TestCashPaymentMethodId}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var method = await getResponse.Content.ReadFromJsonAsync<PaymentMethodDto>();
        method!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_DeletesUnusedPaymentMethod()
    {
        // Create a new method (not used in any payments)
        var createRequest = new CreatePaymentMethodRequest(
            Name: $"Unused-{Guid.NewGuid():N}",
            MethodType: "voucher");

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payment-methods",
            createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<PaymentMethodDto>();

        // Delete it
        var deleteResponse = await _client.DeleteAsync(
            $"/api/locations/{_fixture.TestLocationId}/payment-methods/{created!.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's deleted
        var getResponse = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/payment-methods/{created.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
