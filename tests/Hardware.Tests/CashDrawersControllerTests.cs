using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Hardware.Api.Dtos;
using DarkVelocity.Shared.Contracts.Hal;
using FluentAssertions;

namespace DarkVelocity.Hardware.Tests;

public class CashDrawersControllerTests : IClassFixture<HardwareApiFixture>
{
    private readonly HardwareApiFixture _fixture;
    private readonly HttpClient _client;

    public CashDrawersControllerTests(HardwareApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAll_ReturnsCashDrawers()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/cash-drawers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<CashDrawerDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_FiltersByActive()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/cash-drawers?activeOnly=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<CashDrawerDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().OnlyContain(d => d.IsActive);
    }

    [Fact]
    public async Task GetById_ReturnsCashDrawer()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/cash-drawers/{_fixture.TestCashDrawerId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var drawer = await response.Content.ReadFromJsonAsync<CashDrawerDto>();
        drawer.Should().NotBeNull();
        drawer!.Id.Should().Be(_fixture.TestCashDrawerId);
        drawer.Name.Should().Be("Main Drawer");
        drawer.PrinterId.Should().Be(_fixture.TestPrinterId);
        drawer.ConnectionType.Should().Be("printer");
    }

    [Fact]
    public async Task GetById_NotFound_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/cash-drawers/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_CreatesCashDrawerWithPrinter()
    {
        var request = new CreateCashDrawerRequest(
            Name: "New Drawer",
            PrinterId: _fixture.TestPrinterId,
            KickPulsePin: 0,
            KickPulseOnTime: 120,
            KickPulseOffTime: 120);

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/cash-drawers",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var drawer = await response.Content.ReadFromJsonAsync<CashDrawerDto>();
        drawer.Should().NotBeNull();
        drawer!.Name.Should().Be("New Drawer");
        drawer.PrinterId.Should().Be(_fixture.TestPrinterId);
        drawer.ConnectionType.Should().Be("printer");
        drawer.KickPulseOnTime.Should().Be(120);
    }

    [Fact]
    public async Task Create_CreatesCashDrawerWithNetwork()
    {
        var request = new CreateCashDrawerRequest(
            Name: "Network Drawer",
            ConnectionType: "network",
            IpAddress: "192.168.1.150",
            Port: 5000);

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/cash-drawers",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var drawer = await response.Content.ReadFromJsonAsync<CashDrawerDto>();
        drawer!.ConnectionType.Should().Be("network");
        drawer.IpAddress.Should().Be("192.168.1.150");
        drawer.PrinterId.Should().BeNull();
    }

    [Fact]
    public async Task Create_DuplicateName_ReturnsConflict()
    {
        var request = new CreateCashDrawerRequest(
            Name: "Main Drawer");  // Already exists

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/cash-drawers",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_InvalidPrinter_ReturnsBadRequest()
    {
        var request = new CreateCashDrawerRequest(
            Name: "Invalid Printer Drawer",
            PrinterId: Guid.NewGuid());  // Non-existent

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/cash-drawers",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_PrinterWithoutDrawerSupport_ReturnsBadRequest()
    {
        var request = new CreateCashDrawerRequest(
            Name: "Kitchen Drawer",
            PrinterId: _fixture.TestKitchenPrinterId);  // Kitchen printer doesn't support drawer

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/cash-drawers",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_UpdatesCashDrawer()
    {
        // Create a drawer to update
        var createRequest = new CreateCashDrawerRequest(
            Name: $"Update-Test-{Guid.NewGuid():N}");

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/cash-drawers",
            createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CashDrawerDto>();

        // Update it
        var updateRequest = new UpdateCashDrawerRequest(
            Name: "Updated Drawer",
            KickPulseOnTime: 150);

        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/cash-drawers/{created!.Id}",
            updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await response.Content.ReadFromJsonAsync<CashDrawerDto>();
        updated!.Name.Should().Be("Updated Drawer");
        updated.KickPulseOnTime.Should().Be(150);
    }

    [Fact]
    public async Task Update_AssignPrinter_SetsConnectionType()
    {
        // Create a drawer without printer
        var createRequest = new CreateCashDrawerRequest(
            Name: $"NoPrinter-{Guid.NewGuid():N}",
            ConnectionType: "usb");

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/cash-drawers",
            createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CashDrawerDto>();

        // Assign a printer
        var updateRequest = new UpdateCashDrawerRequest(
            PrinterId: _fixture.TestPrinterId);

        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/cash-drawers/{created!.Id}",
            updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await response.Content.ReadFromJsonAsync<CashDrawerDto>();
        updated!.PrinterId.Should().Be(_fixture.TestPrinterId);
        updated.ConnectionType.Should().Be("printer");
    }

    [Fact]
    public async Task Delete_DeactivatesDrawerWithReferences()
    {
        // The test drawer is referenced by a device
        var response = await _client.DeleteAsync(
            $"/api/locations/{_fixture.TestLocationId}/cash-drawers/{_fixture.TestCashDrawerId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's deactivated, not deleted
        var getResponse = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/cash-drawers/{_fixture.TestCashDrawerId}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var drawer = await getResponse.Content.ReadFromJsonAsync<CashDrawerDto>();
        drawer!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_DeletesUnusedDrawer()
    {
        // Create a drawer with no references
        var createRequest = new CreateCashDrawerRequest(
            Name: $"ToDelete-{Guid.NewGuid():N}");

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/cash-drawers",
            createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CashDrawerDto>();

        // Delete it
        var response = await _client.DeleteAsync(
            $"/api/locations/{_fixture.TestLocationId}/cash-drawers/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's actually deleted
        var getResponse = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/cash-drawers/{created.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
