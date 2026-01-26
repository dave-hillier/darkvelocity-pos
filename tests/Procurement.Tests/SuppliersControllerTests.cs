using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Procurement.Api.Dtos;
using DarkVelocity.Shared.Contracts.Hal;
using FluentAssertions;

namespace DarkVelocity.Procurement.Tests;

public class SuppliersControllerTests : IClassFixture<ProcurementApiFixture>
{
    private readonly ProcurementApiFixture _fixture;
    private readonly HttpClient _client;

    public SuppliersControllerTests(ProcurementApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAll_ReturnsSuppliers()
    {
        var response = await _client.GetAsync("/api/suppliers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<SupplierDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_FiltersByActive()
    {
        var response = await _client.GetAsync("/api/suppliers?activeOnly=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<SupplierDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().OnlyContain(s => s.IsActive);
    }

    [Fact]
    public async Task GetById_ReturnsSupplier()
    {
        var response = await _client.GetAsync($"/api/suppliers/{_fixture.TestSupplierId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var supplier = await response.Content.ReadFromJsonAsync<SupplierDto>();
        supplier.Should().NotBeNull();
        supplier!.Id.Should().Be(_fixture.TestSupplierId);
        supplier.Code.Should().Be("SUP-001");
        supplier.Name.Should().Be("Fresh Foods Ltd");
    }

    [Fact]
    public async Task GetById_NotFound_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/suppliers/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_CreatesSupplier()
    {
        var request = new CreateSupplierRequest(
            Code: $"SUP-{Guid.NewGuid():N}",
            Name: "New Supplier",
            ContactName: "Jane Doe",
            ContactEmail: "jane@supplier.com",
            PaymentTermsDays: 14,
            LeadTimeDays: 2);

        var response = await _client.PostAsJsonAsync("/api/suppliers", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var supplier = await response.Content.ReadFromJsonAsync<SupplierDto>();
        supplier.Should().NotBeNull();
        supplier!.Name.Should().Be("New Supplier");
        supplier.ContactName.Should().Be("Jane Doe");
        supplier.PaymentTermsDays.Should().Be(14);
        supplier.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Create_DuplicateCode_ReturnsConflict()
    {
        var request = new CreateSupplierRequest(
            Code: "SUP-001", // Already exists
            Name: "Duplicate Supplier");

        var response = await _client.PostAsJsonAsync("/api/suppliers", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_UpdatesSupplier()
    {
        // First create a supplier
        var createRequest = new CreateSupplierRequest(
            Code: $"UPDATE-{Guid.NewGuid():N}",
            Name: "Update Test");

        var createResponse = await _client.PostAsJsonAsync("/api/suppliers", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<SupplierDto>();

        // Update it
        var updateRequest = new UpdateSupplierRequest(
            Name: "Updated Name",
            PaymentTermsDays: 45);

        var response = await _client.PutAsJsonAsync($"/api/suppliers/{created!.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await response.Content.ReadFromJsonAsync<SupplierDto>();
        updated!.Name.Should().Be("Updated Name");
        updated.PaymentTermsDays.Should().Be(45);
    }

    [Fact]
    public async Task Delete_DeactivatesSupplier()
    {
        // First create a supplier
        var createRequest = new CreateSupplierRequest(
            Code: $"DELETE-{Guid.NewGuid():N}",
            Name: "Delete Test");

        var createResponse = await _client.PostAsJsonAsync("/api/suppliers", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<SupplierDto>();

        // Delete (soft delete)
        var response = await _client.DeleteAsync($"/api/suppliers/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's deactivated
        var getResponse = await _client.GetAsync($"/api/suppliers/{created.Id}");
        var deactivated = await getResponse.Content.ReadFromJsonAsync<SupplierDto>();
        deactivated!.IsActive.Should().BeFalse();
    }

    // Supplier Ingredients Tests
    [Fact]
    public async Task GetIngredients_ReturnsSupplierIngredients()
    {
        var response = await _client.GetAsync($"/api/suppliers/{_fixture.TestSupplierId}/ingredients");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<SupplierIngredientDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AddIngredient_AddsIngredientToSupplier()
    {
        var newIngredientId = Guid.NewGuid();
        var request = new AddSupplierIngredientRequest(
            IngredientId: newIngredientId,
            LastKnownPrice: 15.00m,
            SupplierProductCode: "PROD-123",
            PackSize: 2.5m,
            PackUnit: "kg",
            IsPreferred: true);

        var response = await _client.PostAsJsonAsync(
            $"/api/suppliers/{_fixture.TestSupplierId}/ingredients",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var ingredient = await response.Content.ReadFromJsonAsync<SupplierIngredientDto>();
        ingredient!.IngredientId.Should().Be(newIngredientId);
        ingredient.LastKnownPrice.Should().Be(15.00m);
        ingredient.PackSize.Should().Be(2.5m);
    }

    [Fact]
    public async Task AddIngredient_DuplicateIngredient_ReturnsConflict()
    {
        // Try to add the same ingredient again
        var request = new AddSupplierIngredientRequest(
            IngredientId: _fixture.TestIngredientId, // Already linked
            LastKnownPrice: 10.00m);

        var response = await _client.PostAsJsonAsync(
            $"/api/suppliers/{_fixture.TestSupplierId}/ingredients",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateIngredient_UpdatesPrice()
    {
        // First get the supplier ingredients
        var getResponse = await _client.GetAsync($"/api/suppliers/{_fixture.TestSupplierId}/ingredients");
        var collection = await getResponse.Content.ReadFromJsonAsync<HalCollection<SupplierIngredientDto>>();
        var existingIngredient = collection!.Embedded.Items.First();

        // Update it
        var request = new UpdateSupplierIngredientRequest(
            LastKnownPrice: 30.00m);

        var response = await _client.PutAsJsonAsync(
            $"/api/suppliers/{_fixture.TestSupplierId}/ingredients/{existingIngredient.Id}",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await response.Content.ReadFromJsonAsync<SupplierIngredientDto>();
        updated!.LastKnownPrice.Should().Be(30.00m);
        updated.LastPriceUpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }
}
